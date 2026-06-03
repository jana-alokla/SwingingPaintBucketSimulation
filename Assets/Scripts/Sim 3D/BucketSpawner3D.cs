using Unity.Mathematics;
using UnityEngine;

public class BucketSpawner3D : MonoBehaviour
{
    [Header("Bucket Shape")]
    public float bucketHeight = 1.4f;
    public float bottomRadius = 0.42f;
    public float topRadius = 0.55f;

    [Header("Paint Fill")]
    [Tooltip("How much of the bucket is filled with paint from the bottom upward.")]
    public float fillHeight = 0.45f;

    [Tooltip("Distance between particles. Smaller value means more particles.")]
    public float particleSpacing = 0.08f;

    [Tooltip("Small random offset to avoid a perfectly rigid grid. Keep it very small.")]
    public float jitterStrength = 0.005f;

    [Header("Initial Velocity")]
    public float3 initialVel;

    [Header("Debug")]
    public bool showSpawnBounds = true;
    public int debug_numParticles;

    public SpawnData GetSpawnData()
    {
        Vector3[] tempPoints = GenerateBucketPoints();
        int numPoints = tempPoints.Length;

        float3[] points = new float3[numPoints];
        float3[] velocities = new float3[numPoints];

        for (int i = 0; i < numPoints; i++)
        {
            points[i] = tempPoints[i];
            velocities[i] = initialVel;
        }

        debug_numParticles = numPoints;

        return new SpawnData()
        {
            points = points,
            velocities = velocities
        };
    }

    private Vector3[] GenerateBucketPoints()
    {
        /*
         * The bucket mesh is centered around Y = 0:
         * bottomY = -height / 2
         * topY    = +height / 2
         *
         * Paint is generated from bottomY upward by fillHeight.
         */

        float bottomY = -bucketHeight * 0.5f;
        float topFillY = Mathf.Min(bottomY + fillHeight, bucketHeight * 0.5f);

        System.Collections.Generic.List<Vector3> points =
            new System.Collections.Generic.List<Vector3>();

        float spacing = Mathf.Max(0.01f, particleSpacing);

        for (float y = bottomY + spacing * 0.5f; y <= topFillY; y += spacing)
        {
            float heightT = Mathf.Clamp01((y - bottomY) / bucketHeight);

            float radiusAtY = Mathf.Lerp(bottomRadius, topRadius, heightT);

            // Keep particles slightly away from the wall at spawn time.
            radiusAtY *= 0.88f;

            for (float x = -radiusAtY; x <= radiusAtY; x += spacing)
            {
                for (float z = -radiusAtY; z <= radiusAtY; z += spacing)
                {
                    Vector2 horizontal = new Vector2(x, z);

                    if (horizontal.magnitude <= radiusAtY)
                    {
                        Vector3 localPoint = new Vector3(x, y, z);

                        if (jitterStrength > 0f)
                        {
                            localPoint += UnityEngine.Random.insideUnitSphere * jitterStrength;
                        }

                        Vector3 worldPoint = transform.TransformPoint(localPoint);
                        points.Add(worldPoint);
                    }
                }
            }
        }

        return points.ToArray();
    }

    public struct SpawnData
    {
        public float3[] points;
        public float3[] velocities;
    }

    private void OnValidate()
    {
        debug_numParticles = EstimateParticleCount();
    }

    private int EstimateParticleCount()
    {
        float bottomY = -bucketHeight * 0.5f;
        float topFillY = Mathf.Min(bottomY + fillHeight, bucketHeight * 0.5f);

        int count = 0;
        float spacing = Mathf.Max(0.01f, particleSpacing);

        for (float y = bottomY + spacing * 0.5f; y <= topFillY; y += spacing)
        {
            float heightT = Mathf.Clamp01((y - bottomY) / bucketHeight);
            float radiusAtY = Mathf.Lerp(bottomRadius, topRadius, heightT) * 0.88f;

            for (float x = -radiusAtY; x <= radiusAtY; x += spacing)
            {
                for (float z = -radiusAtY; z <= radiusAtY; z += spacing)
                {
                    if (new Vector2(x, z).magnitude <= radiusAtY)
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }

    private void OnDrawGizmos()
    {
        if (!showSpawnBounds)
        {
            return;
        }

        float bottomY = -bucketHeight * 0.5f;
        float topFillY = Mathf.Min(bottomY + fillHeight, bucketHeight * 0.5f);

        Gizmos.color = new Color(0f, 0.6f, 1f, 0.6f);

        int rings = 12;

        for (int r = 0; r <= rings; r++)
        {
            float t = r / (float)rings;
            float y = Mathf.Lerp(bottomY, topFillY, t);

            float heightT = Mathf.Clamp01((y - bottomY) / bucketHeight);
            float radius = Mathf.Lerp(bottomRadius, topRadius, heightT) * 0.88f;

            DrawCircle(transform.TransformPoint(new Vector3(0f, y, 0f)), radius);
        }
    }

    private void DrawCircle(Vector3 center, float radius)
    {
        int segments = 48;
        Vector3 previous = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;

            Vector3 localPoint =
                new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            Vector3 worldPoint =
                transform.TransformPoint(localPoint + transform.InverseTransformPoint(center));

            if (i > 0)
            {
                Gizmos.DrawLine(previous, worldPoint);
            }

            previous = worldPoint;
        }
    }
}