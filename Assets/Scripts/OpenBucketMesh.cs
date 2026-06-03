using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class OpenBucketMesh : MonoBehaviour
{
    [Header("Bucket Shape")]
    public int segments = 64;
    public float height = 1.4f;
    public float topRadius = 0.55f;
    public float bottomRadius = 0.42f;

    [Header("Bottom Hole")]
    public float holeRadius = 0.08f;

    [Header("Visual")]
    public bool rebuildInEditor = true;

    private void Start()
    {
        BuildBucket();
    }

    private void OnValidate()
    {
        if (rebuildInEditor)
        {
            BuildBucket();
        }
    }

    public void BuildBucket()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        Mesh mesh = new Mesh();
        mesh.name = "Open Bucket Mesh Double Sided";

        int sideVertexCount = segments * 2;
        int bottomOuterStart = sideVertexCount;
        int bottomInnerStart = bottomOuterStart + segments;

        Vector3[] vertices = new Vector3[segments * 4];

        float halfHeight = height * 0.5f;
        float bottomY = -halfHeight;
        float topY = halfHeight;

        // نقاط الجدار: حلقة تحت وحلقة فوق
        for (int i = 0; i < segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices[i] = new Vector3(cos * bottomRadius, bottomY, sin * bottomRadius);
            vertices[i + segments] = new Vector3(cos * topRadius, topY, sin * topRadius);
        }

        // نقاط القاع: حلقة خارجية وحلقة داخلية للفتحة
        for (int i = 0; i < segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices[bottomOuterStart + i] = new Vector3(cos * bottomRadius, bottomY, sin * bottomRadius);
            vertices[bottomInnerStart + i] = new Vector3(cos * holeRadius, bottomY, sin * holeRadius);
        }

        // مثلثات الوجه الخارجي، ثم نكررها بالعكس للوجه الداخلي
        int singleSideTriangleCount = segments * 4 * 3;
        int[] triangles = new int[singleSideTriangleCount * 2];

        int t = 0;

        // الجدار الجانبي
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            int bottomCurrent = i;
            int bottomNext = next;
            int topCurrent = i + segments;
            int topNext = next + segments;

            // المثلث الأول من الجدار
            triangles[t++] = bottomCurrent;
            triangles[t++] = bottomNext;
            triangles[t++] = topCurrent;

            // المثلث الثاني من الجدار
            triangles[t++] = bottomNext;
            triangles[t++] = topNext;
            triangles[t++] = topCurrent;
        }

        // القاع مع فتحة بالنص
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            int outerCurrent = bottomOuterStart + i;
            int outerNext = bottomOuterStart + next;
            int innerCurrent = bottomInnerStart + i;
            int innerNext = bottomInnerStart + next;

            // المثلث الأول من القاع
            triangles[t++] = outerCurrent;
            triangles[t++] = innerCurrent;
            triangles[t++] = outerNext;

            // المثلث الثاني من القاع
            triangles[t++] = innerCurrent;
            triangles[t++] = innerNext;
            triangles[t++] = outerNext;
        }

        // ننسخ كل المثلثات بالعكس حتى يصير الدلو مرئي من الداخل والخارج
        int originalTriangleCount = t;

        for (int i = 0; i < originalTriangleCount; i += 3)
        {
            triangles[t++] = triangles[i];
            triangles[t++] = triangles[i + 2];
            triangles[t++] = triangles[i + 1];
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;
    }
}