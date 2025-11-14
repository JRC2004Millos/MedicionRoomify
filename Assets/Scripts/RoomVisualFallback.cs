using UnityEngine;
using System.Linq;

public class RoomVisualFallback : MonoBehaviour
{
    [Header("Ejecución")]
    [Tooltip("Si está activo, corre automáticamente en Start. Si no, llama ApplyFallbacks() desde tu RoomBuilder.")]
    public bool runOnStart = true;

    [Header("Materiales predeterminados (opcional). Si no se asignan, se crean en runtime.")]
    public Material wallDefaultMat;
    public Material floorDefaultMat;
    public Material ceilingDefaultMat;

    [Header("Detección")]
    [Tooltip("Nombres o tags para encontrar cada parte. Se comparan en lowercase y por 'contiene'.")]
    public string[] floorKeywords = new[] { "floor", "piso" };
    public string[] wallKeywords = new[] { "wall", "pared" };
    public string[] ceilKeywords = new[] { "ceiling", "techo" };

    [Header("Techo")]
    [Tooltip("Altura del techo si no se puede inferir desde las paredes.")]
    public float defaultCeilingHeight = 2.6f;
    [Tooltip("Crear techo si no existe en la jerarquía.")]
    public bool createCeilingIfMissing = true;
    [Tooltip("Nombre del GameObject que se creará para el techo.")]
    public string ceilingObjectName = "Ceiling";

    void Start()
    {
        if (runOnStart) ApplyFallbacks();
    }

    public void ApplyFallbacks()
    {
        EnsureDefaultMaterials();

        // 1) Buscar renderers del cuarto
        var renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
        var floors = renderers.Where(r => NameMatches(r, floorKeywords)).ToList();
        var walls = renderers.Where(r => NameMatches(r, wallKeywords)).ToList();
        var ceils = renderers.Where(r => NameMatches(r, ceilKeywords)).ToList();

        foreach (var r in floors) AssignIfMissing(r, floorDefaultMat);
        foreach (var r in walls) AssignIfMissing(r, wallDefaultMat);
        foreach (var r in ceils) AssignIfMissing(r, ceilingDefaultMat);

        if (createCeilingIfMissing && ceils.Count == 0)
        {
            var floorCandidate = floors
                .Select(r => r.GetComponent<MeshFilter>())
                .Where(mf => mf && mf.sharedMesh)
                .OrderByDescending(mf => ApproxMeshAreaOnPlaneY(mf.sharedMesh, transform))
                .FirstOrDefault();

            if (floorCandidate != null)
            {
                float height = InferCeilingHeightFromWalls(walls, floorCandidate.transform) ?? defaultCeilingHeight;
                var ceilGO = BuildCeilingFromFloor(floorCandidate, height);
                if (ceilGO != null)
                {
                    var mr = ceilGO.GetComponent<MeshRenderer>();
                    AssignIfMissing(mr, ceilingDefaultMat);
                }
            }
        }
    }

    void EnsureDefaultMaterials()
    {
        if (floorDefaultMat == null)
            floorDefaultMat = MakeCheckerMat("Floor_Fallback_Mat", 0.25f, 512);

        if (wallDefaultMat == null)
            wallDefaultMat = MakeColorMat("Wall_Fallback_Mat", new Color(0.85f, 0.85f, 0.88f));

        if (ceilingDefaultMat == null)
            ceilingDefaultMat = MakeColorMat("Ceiling_Fallback_Mat", new Color(0.95f, 0.95f, 0.95f));
    }

    void AssignIfMissing(MeshRenderer r, Material fallback)
    {
        if (r == null) return;
        var mats = r.sharedMaterials;

        if (mats == null || mats.Length == 0)
        {
            r.sharedMaterial = fallback;
            return;
        }

        if (mats.Length == 1)
        {
            if (IsMissingOrDefault(mats[0])) r.sharedMaterial = fallback;
            return;
        }

        for (int i = 0; i < mats.Length; i++)
        {
            if (!IsMissingOrDefault(mats[i])) continue;

            if (i == 0) mats[i] = wallDefaultMat;
            else if (i == 1) mats[i] = floorDefaultMat;
            else mats[i] = ceilingDefaultMat;
        }
        r.sharedMaterials = mats;
    }

    bool IsMissingOrDefault(Material m)
    {
        if (m == null) return true;
        bool noTex = m.mainTexture == null;
        bool defaultName = m.name.ToLower().Contains("default");
        return noTex || defaultName;
    }

    bool NameMatches(Component c, string[] keywords)
    {
        string n = c.gameObject.name.ToLower();
        string t = c.tag != null ? c.tag.ToLower() : "";
        return keywords.Any(k => n.Contains(k.ToLower()) || t.Contains(k.ToLower()));
    }

    Material MakeColorMat(string name, Color color)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = name;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        return mat;
    }

    Material MakeCheckerMat(string name, float tiling, int size)
    {
        var tex = MakeCheckerTexture(size, size);
        tex.wrapMode = TextureWrapMode.Repeat;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = name;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_BaseMap"))
        {
            mat.mainTextureScale = new Vector2(tiling, tiling);
        }
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
        return mat;
    }


    Texture2D MakeCheckerTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        int squares = 8;
        Color c1 = new Color(0.72f, 0.72f, 0.72f);
        Color c2 = new Color(0.45f, 0.45f, 0.45f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool odd = ((x / (w / squares)) + (y / (h / squares))) % 2 == 1;
                tex.SetPixel(x, y, odd ? c1 : c2);
            }
        tex.Apply();
        return tex;
    }

    GameObject BuildCeilingFromFloor(MeshFilter floorMF, float ceilingHeight)
    {
        if (floorMF == null || floorMF.sharedMesh == null) return null;

        var parent = floorMF.transform.parent != null ? floorMF.transform.parent : transform;

        var floorMesh = floorMF.sharedMesh;
        var vLocal = floorMesh.vertices;
        var vWorld = new Vector3[vLocal.Length];
        for (int i = 0; i < vLocal.Length; i++)
            vWorld[i] = floorMF.transform.TransformPoint(vLocal[i]);

        var vTopWorld = new Vector3[vWorld.Length];
        for (int i = 0; i < vWorld.Length; i++)
            vTopWorld[i] = vWorld[i] + Vector3.up * ceilingHeight;

        var vTopParent = new Vector3[vTopWorld.Length];
        for (int i = 0; i < vTopWorld.Length; i++)
            vTopParent[i] = parent.InverseTransformPoint(vTopWorld[i]);

        var tris = floorMesh.triangles;

        Vector2[] uv = (floorMesh.uv != null && floorMesh.uv.Length == vLocal.Length) ? floorMesh.uv : null;

        var ceilingMesh = new Mesh { name = "CeilingMesh" };
        ceilingMesh.indexFormat = (vTopParent.Length > 65000)
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        ceilingMesh.vertices = vTopParent;
        ceilingMesh.triangles = tris;
        if (uv != null) ceilingMesh.uv = uv;
        ceilingMesh.RecalculateNormals();
        ceilingMesh.RecalculateBounds();
        ceilingMesh.RecalculateTangents();

        var go = new GameObject(ceilingObjectName, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var mf = go.GetComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>();
        mf.sharedMesh = ceilingMesh;

        var mcol = go.GetComponent<MeshCollider>();
        mcol.sharedMesh = ceilingMesh;
        mcol.convex = false;

        AssignIfMissing(mr, ceilingDefaultMat);
        var m = mr.sharedMaterial;
        if (m != null)
        {
            if (m.shader == null || !m.shader.name.Contains("Universal Render Pipeline"))
                m.shader = Shader.Find("Universal Render Pipeline/Lit");
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
            if (m.HasProperty("_BaseColor") && m.color.a < 1f)
                m.SetColor("_BaseColor", new Color(m.color.r, m.color.g, m.color.b, 1f));
        }

        return go;
    }


    float GetMeshMeanY(Vector3[] vertices)
    {
        if (vertices == null || vertices.Length == 0) return 0f;
        float s = 0f;
        for (int i = 0; i < vertices.Length; i++) s += vertices[i].y;
        return s / vertices.Length;
    }

    float ApproxMeshAreaOnPlaneY(Mesh mesh, Transform t)
    {
        var b = mesh.bounds;
        return (b.size.x * b.size.z);
    }

    float? InferCeilingHeightFromWalls(System.Collections.Generic.List<MeshRenderer> walls, Transform reference)
    {
        if (walls == null || walls.Count == 0) return null;

        var biggest = walls.OrderByDescending(w => w.bounds.size.x * w.bounds.size.y * w.bounds.size.z).First();
        var worldBounds = biggest.bounds;

        float heightWorld = worldBounds.size.y;

        var topWorld = new Vector3(worldBounds.center.x, worldBounds.max.y, worldBounds.center.z);
        var bottomWorld = new Vector3(worldBounds.center.x, worldBounds.min.y, worldBounds.center.z);
        var topLocal = reference.InverseTransformPoint(topWorld);
        var bottomLocal = reference.InverseTransformPoint(bottomWorld);

        float heightLocal = Mathf.Abs(topLocal.y - bottomLocal.y);
        return (heightLocal > 0.2f && heightLocal < 6f) ? heightLocal : (float?)null;
    }
}
