using UnityEngine;
using Unity.Mathematics;

public class BucketSimulation3D : MonoBehaviour
{
    public event System.Action SimulationStepCompleted;

    [Header("Settings")]
    public float timeScale = 1;
    public bool fixedTimeStep;
    public int iterationsPerFrame = 3;
    public float gravity = -10;
    [Range(0, 1)] public float collisionDamping = 0.05f;
    public float smoothingRadius = 0.2f;
    public float targetDensity = 25f;
    public float pressureMultiplier = 35f;
    public float nearPressureMultiplier = 3f;
    public float viscosityStrength = 0.15f;

    [Header("Bucket Container Shape")]
    public float bucketHeight = 1.4f;
    public float bucketBottomRadius = 0.42f;
    public float bucketTopRadius = 0.55f;

    [Header("References")]
    public ComputeShader compute;
    public BucketSpawner3D spawner;
    public ParticleDisplay3D display;
    public Transform floorDisplay;

    // Buffers
    public ComputeBuffer positionBuffer { get; private set; }
    public ComputeBuffer velocityBuffer { get; private set; }
    public ComputeBuffer densityBuffer { get; private set; }
    public ComputeBuffer predictedPositionsBuffer { get; private set; }

    private ComputeBuffer spatialIndices;
    private ComputeBuffer spatialOffsets;
    private ComputeBuffer particleStateBuffer;


    // Kernel IDs
    private const int externalForcesKernel = 0;
    private const int spatialHashKernel = 1;
    private const int densityKernel = 2;
    private const int pressureKernel = 3;
    private const int viscosityKernel = 4;
    private const int updatePositionsKernel = 5;

    private GPUSort gpuSort;

    // State
    private bool isPaused;
    private BucketSpawner3D.SpawnData spawnData;

    private void Start()
    {
        Debug.Log("Bucket fluid simulation started.");

        float deltaTime = 1f / 60f;
        Time.fixedDeltaTime = deltaTime;

        if (compute == null)
        {
            Debug.LogError("BucketSimulation3D needs FluidSim3D.compute assigned.");
            enabled = false;
            return;
        }

        if (spawner == null)
        {
            Debug.LogError("BucketSimulation3D needs BucketSpawner3D assigned.");
            enabled = false;
            return;
        }

        if (display == null)
        {
            Debug.LogError("BucketSimulation3D needs ParticleDisplay3D assigned.");
            enabled = false;
            return;
        }

        spawnData = spawner.GetSpawnData();

        int numParticles = spawnData.points.Length;

        if (numParticles <= 0)
        {
            Debug.LogError("BucketSpawner3D generated 0 particles. Check fillHeight and particleSpacing.");
            enabled = false;
            return;
        }

        positionBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numParticles);
        predictedPositionsBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numParticles);
        velocityBuffer = ComputeHelper.CreateStructuredBuffer<float3>(numParticles);
        densityBuffer = ComputeHelper.CreateStructuredBuffer<float2>(numParticles);
        spatialIndices = ComputeHelper.CreateStructuredBuffer<uint3>(numParticles);
        spatialOffsets = ComputeHelper.CreateStructuredBuffer<uint>(numParticles);
        particleStateBuffer = ComputeHelper.CreateStructuredBuffer<uint>(numParticles);

        SetInitialBufferData(spawnData);

        SetInitialParticleStates(numParticles);

        ComputeHelper.SetBuffer(compute, positionBuffer, "Positions", externalForcesKernel, updatePositionsKernel);
        ComputeHelper.SetBuffer(compute, predictedPositionsBuffer, "PredictedPositions", externalForcesKernel, spatialHashKernel, densityKernel, pressureKernel, viscosityKernel, updatePositionsKernel);
        ComputeHelper.SetBuffer(compute, spatialIndices, "SpatialIndices", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compute, spatialOffsets, "SpatialOffsets", spatialHashKernel, densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compute, densityBuffer, "Densities", densityKernel, pressureKernel, viscosityKernel);
        ComputeHelper.SetBuffer(compute, velocityBuffer, "Velocities", externalForcesKernel, pressureKernel, viscosityKernel, updatePositionsKernel);

        ComputeHelper.SetBuffer(
            compute,
            particleStateBuffer,
            "ParticleStates",
            externalForcesKernel,
            spatialHashKernel,
            densityKernel,
            pressureKernel,
            viscosityKernel,
            updatePositionsKernel
        );

        compute.SetInt("numParticles", positionBuffer.count);

        gpuSort = new GPUSort();
        gpuSort.SetBuffers(spatialIndices, spatialOffsets);

        display.Init(this);
    }

    private void SetInitialParticleStates(int numParticles)
    {
        uint[] states = new uint[numParticles];

        for (int i = 0; i < states.Length; i++)
        {
            states[i] = 1; // 1 means inside bucket
        }

        particleStateBuffer.SetData(states);
    }

    private void FixedUpdate()
    {
        if (fixedTimeStep)
        {
            RunSimulationFrame(Time.fixedDeltaTime);
        }
    }

    private void Update()
    {
        if (!fixedTimeStep && Time.frameCount > 10)
        {
            RunSimulationFrame(Time.deltaTime);
        }

        if (floorDisplay != null)
        {
            floorDisplay.transform.localScale =
                new Vector3(1, 1 / transform.localScale.y * 0.1f, 1);
        }

        // We do not use UnityEngine.Input here because the project uses the new Input System.
    }

    private void RunSimulationFrame(float frameTime)
    {
        if (isPaused)
        {
            return;
        }

        if (iterationsPerFrame <= 0)
        {
            iterationsPerFrame = 1;
        }

        float timeStep = frameTime / iterationsPerFrame * timeScale;

        UpdateSettings(timeStep);

        for (int i = 0; i < iterationsPerFrame; i++)
        {
            RunSimulationStep();
            SimulationStepCompleted?.Invoke();
        }
    }

    private void RunSimulationStep()
    {
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: externalForcesKernel);
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: spatialHashKernel);

        gpuSort.SortAndCalculateOffsets();

        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: densityKernel);
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: pressureKernel);
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: viscosityKernel);
        ComputeHelper.Dispatch(compute, positionBuffer.count, kernelIndex: updatePositionsKernel);
    }

    private void UpdateSettings(float deltaTime)
    {
        Vector3 simBoundsSize = transform.localScale;
        Vector3 simBoundsCentre = transform.position;

        compute.SetFloat("deltaTime", deltaTime);
        compute.SetFloat("gravity", gravity);
        compute.SetFloat("collisionDamping", collisionDamping);
        compute.SetFloat("smoothingRadius", smoothingRadius);
        compute.SetFloat("targetDensity", targetDensity);
        compute.SetFloat("pressureMultiplier", pressureMultiplier);
        compute.SetFloat("nearPressureMultiplier", nearPressureMultiplier);
        compute.SetFloat("viscosityStrength", viscosityStrength);
        compute.SetVector("boundsSize", simBoundsSize);
        compute.SetVector("centre", simBoundsCentre);

        compute.SetMatrix("localToWorld", transform.localToWorldMatrix);
        compute.SetMatrix("worldToLocal", transform.worldToLocalMatrix);

        float bucketBottomY = -bucketHeight * 0.5f;
        float bucketTopY = bucketHeight * 0.5f;

        compute.SetFloat("bucketBottomY", bucketBottomY);
        compute.SetFloat("bucketTopY", bucketTopY);
        compute.SetFloat("bucketBottomRadius", bucketBottomRadius);
        compute.SetFloat("bucketTopRadius", bucketTopRadius);
    }

    private void SetInitialBufferData(BucketSpawner3D.SpawnData data)
    {
        float3[] allPoints = new float3[data.points.Length];
        System.Array.Copy(data.points, allPoints, data.points.Length);

        // Reset all velocities to zero
        float3[] zeroVelocities = new float3[data.points.Length];

        for (int i = 0; i < zeroVelocities.Length; i++)
        {
            zeroVelocities[i] = new float3(0f, 0f, 0f);
        }

        positionBuffer.SetData(allPoints);
        predictedPositionsBuffer.SetData(allPoints);
        velocityBuffer.SetData(zeroVelocities);
    }

    [ContextMenu("Reset Bucket Fluid")]
    public void ResetFluid()
    {
        isPaused = true;

        spawnData = spawner.GetSpawnData();

        SetInitialBufferData(spawnData);
        SetInitialParticleStates(spawnData.points.Length);

        isPaused = false;
    }

    private void OnDestroy()
    {
        ComputeHelper.Release(
            positionBuffer,
            predictedPositionsBuffer,
            velocityBuffer,
            densityBuffer,
            spatialIndices,
            spatialOffsets,
            particleStateBuffer
        );
    }

    private void OnDrawGizmos()
    {
        var oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = oldMatrix;
    }
}