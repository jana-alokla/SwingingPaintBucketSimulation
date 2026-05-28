using UnityEngine;

public struct RopeNode
{
    public Vector3 position;
    public Vector3 previousPosition;
}

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class MassSpringRope : MonoBehaviour
{
    [Header("References")]
    public Transform pivotPoint;
    public Transform ropeAttachPoint;

    [Header("Rope Settings")]
    public float ropeLength = 4.1f;
    public int nodeCount = 45;

    [Header("Verlet Physics")]
    public Vector3 gravity = new Vector3(0f, -9.81f, 0f);

    [Range(0.90f, 1.0f)]
    public float damping = 0.995f;

    [Tooltip("Higher = rope keeps its length better.")]
    public int constraintIterations = 25;

    [Tooltip("Higher = more stable simulation.")]
    public int subSteps = 4;

    [Header("Visual")]
    public float ropeWidth = 0.06f;
    public Material ropeMaterial;

    [Header("Editor Preview")]
    public bool drawInEditor = true;

    private RopeNode[] nodes;
    private LineRenderer lineRenderer;
    private float distanceBetweenNodes;
    private bool initialized;

    private void Awake()
    {
        Setup();
    }

    private void OnEnable()
    {
        Setup();
    }

    private void OnValidate()
    {
        ropeLength = Mathf.Max(0.1f, ropeLength);
        nodeCount = Mathf.Max(3, nodeCount);
        constraintIterations = Mathf.Max(1, constraintIterations);
        subSteps = Mathf.Max(1, subSteps);
        ropeWidth = Mathf.Max(0.001f, ropeWidth);

        if (!Application.isPlaying && drawInEditor)
        {
            DrawEditorPreview();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying && drawInEditor)
        {
            DrawEditorPreview();
        }
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!Ready())
        {
            return;
        }

        if (!initialized || nodes == null || nodes.Length != nodeCount)
        {
            Setup();
        }

        float dt = Time.fixedDeltaTime / subSteps;

        for (int step = 0; step < subSteps; step++)
        {
            SimulateVerlet(dt);

            for (int i = 0; i < constraintIterations; i++)
            {
                ApplyDistanceConstraints();
            }
        }

        UpdateLineRenderer();
    }

    private bool Ready()
    {
        return pivotPoint != null && ropeAttachPoint != null;
    }

    private void Setup()
    {
        lineRenderer = GetComponent<LineRenderer>();
        SetupLineRenderer();

        if (!Ready())
        {
            return;
        }

        nodes = new RopeNode[nodeCount];
        distanceBetweenNodes = ropeLength / (nodeCount - 1);

        Vector3 start = pivotPoint.position;
        Vector3 end = ropeAttachPoint.position;

        for (int i = 0; i < nodeCount; i++)
        {
            float t = (float)i / (nodeCount - 1);

            Vector3 position = Vector3.Lerp(start, end, t);

            nodes[i].position = position;
            nodes[i].previousPosition = position;
        }

        initialized = true;
        UpdateLineRenderer();
    }

    private void SimulateVerlet(float dt)
    {
        int last = nodeCount - 1;

        // تثبيت أول وآخر نقطة
        nodes[0].position = pivotPoint.position;
        nodes[0].previousPosition = pivotPoint.position;

        nodes[last].position = ropeAttachPoint.position;
        nodes[last].previousPosition = ropeAttachPoint.position;

        Vector3 gravityStep = gravity * (dt * dt);

        // نحرك فقط النقاط الوسطية
        for (int i = 1; i < last; i++)
        {
            Vector3 currentPosition = nodes[i].position;
            Vector3 previousPosition = nodes[i].previousPosition;

            Vector3 velocity = (currentPosition - previousPosition) * damping;

            Vector3 newPosition = currentPosition + velocity + gravityStep;

            nodes[i].previousPosition = currentPosition;
            nodes[i].position = newPosition;
        }

        // إعادة تثبيت الأطراف بعد الحركة
        nodes[0].position = pivotPoint.position;
        nodes[last].position = ropeAttachPoint.position;
    }

    private void ApplyDistanceConstraints()
    {
        int last = nodeCount - 1;

        // تثبيت الطرفين
        nodes[0].position = pivotPoint.position;
        nodes[last].position = ropeAttachPoint.position;

        for (int i = 0; i < nodeCount - 1; i++)
        {
            Vector3 p1 = nodes[i].position;
            Vector3 p2 = nodes[i + 1].position;

            Vector3 delta = p2 - p1;
            float currentDistance = delta.magnitude;

            if (currentDistance < 0.0001f)
            {
                continue;
            }

            float difference = currentDistance - distanceBetweenNodes;
            Vector3 correction = delta.normalized * difference;

            bool firstIsPinned = i == 0;
            bool secondIsPinned = i + 1 == last;

            if (firstIsPinned && !secondIsPinned)
            {
                nodes[i + 1].position -= correction;
            }
            else if (!firstIsPinned && secondIsPinned)
            {
                nodes[i].position += correction;
            }
            else if (!firstIsPinned && !secondIsPinned)
            {
                nodes[i].position += correction * 0.5f;
                nodes[i + 1].position -= correction * 0.5f;
            }
        }

        // تثبيت نهائي للطرفين
        nodes[0].position = pivotPoint.position;
        nodes[last].position = ropeAttachPoint.position;
    }

    private void SetupLineRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.positionCount = nodeCount;
        lineRenderer.startWidth = ropeWidth;
        lineRenderer.endWidth = ropeWidth;

        lineRenderer.numCornerVertices = 5;
        lineRenderer.numCapVertices = 5;

        if (ropeMaterial != null)
        {
            lineRenderer.sharedMaterial = ropeMaterial;
        }
    }

    private void UpdateLineRenderer()
    {
        if (lineRenderer == null || nodes == null)
        {
            return;
        }

        SetupLineRenderer();

        for (int i = 0; i < nodeCount; i++)
        {
            lineRenderer.SetPosition(i, nodes[i].position);
        }
    }

    private void DrawEditorPreview()
    {
        if (!Ready())
        {
            return;
        }

        lineRenderer = GetComponent<LineRenderer>();
        SetupLineRenderer();

        Vector3 start = pivotPoint.position;
        Vector3 end = ropeAttachPoint.position;

        for (int i = 0; i < nodeCount; i++)
        {
            float t = (float)i / (nodeCount - 1);
            lineRenderer.SetPosition(i, Vector3.Lerp(start, end, t));
        }
    }

    public void ResetRope()
    {
        initialized = false;
        Setup();
    }
}