using System.Collections.Generic;
using UnityEngine;

// 両端末で同じ保存設定から生成する静的な試作地形。NetworkObjectは増やさない。
[DefaultExecutionOrder(-200)]
public class FanMapLayout : MonoBehaviour
{
    [Min(10f)] public float radius = 40f;
    [Range(30f, 150f)] public float openingAngle = 120f;
    [Range(8, 128)] public int arcSegments = 40;
    [Min(1f)] public float baseHalfWidth = 4f;
    [Min(1f)] public float rearDepth = 4f;
    [Min(0.1f)] public float groundThickness = 0.5f;
    [Min(1f)] public float wallThickness = 2f;
    [Min(4f)] public float wallHeight = 32f;
    [Min(0f)] public float spawnPadding = 0.35f;
    public bool showForest = true;

    private GameObject generated;
    private Mesh groundMesh;
    private PhysicsMaterial boundaryMaterial;
    private readonly List<Material> materials = new List<Material>();
    public Vector3 Origin => transform.position;
    public float GroundHeight => Origin.y;

    private void Awake() => Build();

    public List<Vector3> GetOutline()
    {
        var points = new List<Vector3>
        {
            new Vector3(-baseHalfWidth, 0, -rearDepth), new Vector3(baseHalfWidth, 0, -rearDepth)
        };
        int count = Mathf.Clamp(arcSegments, 8, 128);
        for (int i = 0; i <= count; i++)
        {
            float angle = Mathf.Lerp(openingAngle * 0.5f, -openingAngle * 0.5f, i / (float)count) * Mathf.Deg2Rad;
            points.Add(new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius);
        }
        return points;
    }

    public bool TryGetSpawn(float footprintRadius, float bottomOffset, float scatter, out Vector3 position)
        => TryGetSpawn(footprintRadius, bottomOffset, scatter, Random.value, Random.value, out position);

    // 単位区間のサンプルを幾何へ変換。端点・最大散布も同じ経路で検証できる。
    public bool TryGetSpawn(float footprintRadius, float bottomOffset, float scatter,
        float angleSample, float scatterSample, out Vector3 position)
    {
        position = Origin;
        float clearance = Mathf.Max(0f, footprintRadius) + spawnPadding;
        // 弧の内接弦による小さな差も差し引く。
        float chordInset = radius * (1f - Mathf.Cos(openingAngle * Mathf.Deg2Rad / (2f * Mathf.Clamp(arcSegments, 8, 128))));
        float maximum = radius - clearance - chordInset;
        float band = Mathf.Clamp(scatter, 0f, Mathf.Max(0f, maximum - clearance - 1f));
        float minimum = maximum - band;
        if (minimum <= clearance) return false;
        float safeHalfAngle = openingAngle * 0.5f - Mathf.Asin(Mathf.Clamp01(clearance / minimum)) * Mathf.Rad2Deg;
        if (safeHalfAngle <= 0f) return false;
        float angle = Mathf.Lerp(-safeHalfAngle, safeHalfAngle, Mathf.Clamp01(angleSample)) * Mathf.Deg2Rad;
        float distance = maximum - band * Mathf.Clamp01(scatterSample);
        position = Origin + new Vector3(Mathf.Sin(angle) * distance, bottomOffset + 0.05f, Mathf.Cos(angle) * distance);
        return true;
    }

    public void Build()
    {
        if (generated != null) return;
        generated = new GameObject("Generated Fan Map");
        generated.transform.SetParent(transform, false);
        var outline = GetOutline();
        int count = outline.Count;
        // 砦原点を中心とする上面の扇状三角形＋下面と側面で厚みを付ける。
        var vertices = new List<Vector3> { Vector3.zero, Vector3.down * groundThickness };
        foreach (var point in outline) { vertices.Add(point); vertices.Add(point + Vector3.down * groundThickness); }
        var triangles = new List<int>();
        for (int i = 0; i < count; i++)
        {
            int a = 2 + i * 2, b = 2 + ((i + 1) % count) * 2;
            triangles.AddRange(new[] { 0, b, a, 1, a + 1, b + 1, a, b, b + 1, a, b + 1, a + 1 });
        }
        groundMesh = new Mesh { name = "Fan Map Ground" };
        groundMesh.SetVertices(vertices); groundMesh.SetTriangles(triangles, 0); groundMesh.RecalculateNormals(); groundMesh.RecalculateBounds();
        var floor = new GameObject("Fan Ground", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        floor.transform.SetParent(generated.transform, false);
        floor.GetComponent<MeshFilter>().sharedMesh = groundMesh;
        floor.GetComponent<MeshCollider>().sharedMesh = groundMesh;
        floor.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(new Color(0.27f, 0.36f, 0.21f));
        Material bark = null, leaves = null;
        if (showForest) { bark = MakeMaterial(new Color(0.22f, 0.14f, 0.07f)); leaves = MakeMaterial(new Color(0.08f, 0.23f, 0.10f)); }
        // 押し付け入力で発生する摩擦が重力を支え、壁に張り付くのを防ぐ。床には適用しない。
        boundaryMaterial = new PhysicsMaterial("Fan Boundary No Friction")
        {
            staticFriction = 0f, dynamicFriction = 0f, bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
            hideFlags = HideFlags.DontSave
        };
        for (int i = 0; i < count; i++)
        {
            Vector3 start = outline[i], end = outline[(i + 1) % count];
            Vector3 edge = end - start;
            Vector3 outward = Vector3.Cross(Vector3.up, edge).normalized;
            var wall = new GameObject("Boundary " + i, typeof(BoxCollider));
            wall.transform.SetParent(generated.transform, false);
            wall.transform.localPosition = (start + end) * 0.5f + outward * wallThickness * 0.5f
                + Vector3.up * (wallHeight * 0.5f - groundThickness);
            wall.transform.localRotation = Quaternion.LookRotation(edge.normalized);
            wall.GetComponent<BoxCollider>().size = new Vector3(wallThickness, wallHeight, edge.magnitude + 0.25f);
            wall.GetComponent<BoxCollider>().sharedMaterial = boundaryMaterial;
            // 左右の長い辺だけを森で表す。外側へ置き、移動・弾判定は壁だけが担当する。
            if (showForest && (i == 1 || i == count - 1))
            {
                int trees = Mathf.CeilToInt(edge.magnitude / 1.5f);
                for (int t = 0; t <= trees; t++)
                {
                    Vector3 center = Vector3.Lerp(start, end, t / (float)trees) + outward * (wallThickness + 1.5f);
                    for (int row = 0; row < 2; row++)
                    {
                        Vector3 tree = center + outward * row * 2.5f;
                        AddTreePart(PrimitiveType.Cylinder, tree + Vector3.up * 2f, new Vector3(0.5f, 2f, 0.5f), bark);
                        AddTreePart(PrimitiveType.Sphere, tree + Vector3.up * 4.5f, new Vector3(3.2f, 4f, 3.2f), leaves);
                    }
                }
            }
        }
    }

    private Material MakeMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var material = new Material(shader) { color = color, hideFlags = HideFlags.DontSave };
        materials.Add(material);
        return material;
    }

    private void AddTreePart(PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = "Forest " + type;
        part.transform.SetParent(generated.transform, false); part.transform.localPosition = position; part.transform.localScale = scale;
        var collider = part.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    private void OnDestroy()
    {
        if (groundMesh != null) Destroy(groundMesh);
        if (boundaryMaterial != null) Destroy(boundaryMaterial);
        foreach (var material in materials) if (material != null) Destroy(material);
    }

    private void OnDrawGizmos()
    {
        var outline = GetOutline();
        Gizmos.color = Color.green;
        for (int i = 0; i < outline.Count; i++)
            Gizmos.DrawLine(Origin + outline[i], Origin + outline[(i + 1) % outline.Count]);
    }
}
