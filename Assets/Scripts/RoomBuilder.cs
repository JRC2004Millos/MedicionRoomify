using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(0)]
public class RoomBuilder : MonoBehaviour
{
    [Header("Editor only (pruebas)")]
    [Tooltip("Ruta absoluta al JSON cuando pruebas en el Editor.")]
    public string editorJsonPath;

    [Header("Opciones de render")]
    [Tooltip("Espesor de los muros en metros.")]
    public float wallThickness = 0.05f;
    [Tooltip("Material para el piso (fallback).")]
    public Material floorMaterial;
    [Tooltip("Material para los muros (fallback).")]
    public Material wallMaterial;

    private RoomData data;

    private float floorBaseY = 0f;

    public IReadOnlyList<Vector2> FloorPolygon2D => floorPolygon2D;
    public float FloorBaseY => floorBaseY;
    public float RoomHeightMeters => data != null && data.room_dimensions != null ? data.room_dimensions.height : 2.5f;
    private List<Vector2> floorPolygon2D = new List<Vector2>();


    void Start()
    {
        string path = GetJsonPath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogError($"[RoomBuilder] No se encontró el JSON: {path}");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            data = JsonUtility.FromJson<RoomData>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RoomBuilder] Error leyendo/parsing JSON: {ex.Message}");
            return;
        }

        if (data == null || data.corners == null || data.walls == null)
        {
            Debug.LogError("[RoomBuilder] JSON inválido o incompleto.");
            return;
        }

        // Materiales de respaldo
        if (floorMaterial == null)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = new Color(0.6f, 0.6f, 0.6f);
            floorMaterial = m;
        }
        if (wallMaterial == null)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = Color.white;
            wallMaterial = m;
        }

        var cornersById = data.corners.ToDictionary(c => c.id, c => c);
        var floorVerts2D = OrderPolygonByWalls(data.walls, cornersById);
        if (floorVerts2D == null || floorVerts2D.Count < 3)
        {
            Debug.LogError("[RoomBuilder] No se pudo derivar el polígono del piso.");
            return;
        }
        floorPolygon2D = floorVerts2D.ToList();

        BuildFloor(floorVerts2D);
        BuildWalls(data.walls, cornersById, data.room_dimensions != null ? data.room_dimensions.height : 2.5f);

        FrameCameraToBounds(floorVerts2D);

        var rs = FindFirstObjectByType<RoomSpace>();
        if (rs != null)
        {
            rs.SetFloorPolygonWorldXZ(floorVerts2D);

            rs.minX = floorVerts2D.Min(v => v.x);
            rs.maxX = floorVerts2D.Max(v => v.x);
            rs.minZ = floorVerts2D.Min(v => v.y);
            rs.maxZ = floorVerts2D.Max(v => v.y);
        }

        GetComponent<RoomVisualFallback>()?.ApplyFallbacks();

        Debug.Log("[RoomBuilder] Renderización completada.");
    }

    public bool TryGetSpawnInside(out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;
        if (floorPolygon2D == null || floorPolygon2D.Count < 3) return false;

        // Centroide de área de un polígono simple (fórmula de “shoelace”)
        double A = 0.0; // 2 * area con signo
        double Cx = 0.0;
        double Cz = 0.0;
        int n = floorPolygon2D.Count;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double xi = floorPolygon2D[i].x;
            double zi = floorPolygon2D[i].y;
            double xj = floorPolygon2D[j].x;
            double zj = floorPolygon2D[j].y;
            double cross = xj * zi - xi * zj;
            A += cross;
            Cx += (xj + xi) * cross;
            Cz += (zj + zi) * cross;
        }

        if (Mathf.Approximately((float)A, 0f)) return false; // degenerado

        A *= 0.5;
        double factor = 1.0 / (6.0 * A);
        float cx = (float)(Cx * factor);
        float cz = (float)(Cz * factor);

        worldPoint = new Vector3(cx, floorBaseY, cz);
        return true;
    }

    string GetJsonPath()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Path.Combine(Application.persistentDataPath, "room_data.json");
#else
        return editorJsonPath;
#endif
    }

    List<Vector2> OrderPolygonByWalls(IList<Wall> walls, Dictionary<string, Corner> cornersById)
    {
        var verts = new List<Vector2>();
        if (walls == null || walls.Count == 0) return verts;
        string start = walls[0].from;
        string cur = start;
        int guard = 0;
        while (guard++ < 1024)
        {
            if (!cornersById.ContainsKey(cur)) break;
            var c = cornersById[cur];
            verts.Add(new Vector2(c.position.x, c.position.y));
            var next = walls.FirstOrDefault(w => w.from == cur);
            if (next == null) break;
            cur = next.to;
            if (cur == start) break;
        }
        return verts;
    }

    void BuildFloor(List<Vector2> verts2D)
    {
        if (SignedArea2D(verts2D) > 0f)
            verts2D.Reverse();

        const float Y = 0.05f;
        floorBaseY = Y;

        var verts3D = verts2D.Select(v => new Vector3(v.x, Y, v.y)).ToArray();

        var tris = new List<int>();
        for (int i = 1; i < verts3D.Length - 1; i++)
        {
            tris.Add(0); tris.Add(i); tris.Add(i + 1);
        }

        float minX = verts2D.Min(p => p.x);
        float maxX = verts2D.Max(p => p.x);
        float minZ = verts2D.Min(p => p.y);
        float maxZ = verts2D.Max(p => p.y);
        float spanX = Mathf.Max(0.001f, maxX - minX);
        float spanZ = Mathf.Max(0.001f, maxZ - minZ);

        var uvs = new Vector2[verts3D.Length];
        for (int i = 0; i < verts3D.Length; i++)
        {
            float u = (verts3D[i].x - minX) / spanX;
            float v = (verts3D[i].z - minZ) / spanZ;
            uvs[i] = new Vector2(u, v);
        }

        var mesh = new Mesh { name = "FloorMesh" };
        mesh.indexFormat = verts3D.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32
                                                  : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = verts3D;
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        var go = new GameObject("Floor", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        go.transform.SetParent(this.transform, true);
        go.GetComponent<MeshFilter>().mesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        var mc = go.GetComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        mc.convex = false;

        var textures = FindFirstObjectByType<RoomTextures>();
        if (textures != null)
        {
            textures.ApplyOrQueueRenderer(mr, RoomTextures.SurfaceKind.Floor, Vector3.forward, floorMaterial);
        }
        else
        {
            mr.material = floorMaterial;
            MatTuning.MakeURPLitClean(mr.material);
        }
    }

    void BuildWalls(IList<Wall> walls, Dictionary<string, Corner> cornersById, float height)
    {
        if (walls == null) return;

        foreach (var w in walls)
        {
            if (!cornersById.TryGetValue(w.from, out var a)) continue;
            if (!cornersById.TryGetValue(w.to, out var b)) continue;

            var a3 = new Vector3(a.position.x, 0f, a.position.y);
            var b3 = new Vector3(b.position.x, 0f, b.position.y);
            Vector3 mid = (a3 + b3) * 0.5f;
            Vector3 dir = (b3 - a3).normalized;
            float len = Vector3.Distance(a3, b3);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Wall_{w.from}_{w.to}";
            wall.transform.SetParent(this.transform, true);
            wall.transform.position = mid + Vector3.up * (floorBaseY + height * 0.5f);
            wall.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            wall.transform.localScale = new Vector3(wallThickness, height, len);

            var mr = wall.GetComponent<MeshRenderer>();
            var textures = FindFirstObjectByType<RoomTextures>();

            if (textures != null)
            {
                textures.ApplyOrQueueRenderer(mr, RoomTextures.SurfaceKind.Wall, dir, wallMaterial);
            }
            else
            {
                mr.material = wallMaterial;
                MatTuning.MakeURPLitClean(mr.material);
            }
        }
    }

    void FrameCameraToBounds(List<Vector2> verts2D)
    {
        var center = Vector2.zero;
        foreach (var v in verts2D) center += v;
        center /= verts2D.Count;
        float maxR = verts2D.Max(v => Vector2.Distance(center, v));

        var cam = Camera.main;
        if (cam)
        {
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 100f;
        }
        if (cam == null) return;
        Vector3 c3 = new Vector3(center.x, 0, center.y);
        cam.transform.position = c3 + new Vector3(0, maxR * 2.0f, maxR * 1.6f);
        cam.transform.LookAt(c3 + Vector3.up * 0.5f, Vector3.up);
    }

    float SignedArea2D(List<Vector2> poly)
    {
        double a = 0;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            a += (double)(poly[j].x * poly[i].y - poly[i].x * poly[j].y);
        return (float)(0.5 * a);
    }
}

public static class MatTuning
{
    /// Ajusta materiales URP para asegurar correcta iluminación, sin alterar su tipo (metal/no metal).
    public static void MakeURPLitClean(Material m)
    {
        if (m == null) return;

        // Asegura que use el shader URP/Lit
        if (m.shader == null || m.shader.name.Contains("Standard"))
            m.shader = Shader.Find("Universal Render Pipeline/Lit");

        // Corrige color base / tinte
        if (m.HasProperty("_BaseColor"))
        {
            var baseCol = m.GetColor("_BaseColor");
            baseCol.a = 1f; // fuerza opacidad
            m.SetColor("_BaseColor", baseCol);
        }

        // Elimina color azul por reflexiones ambientales
        if (m.HasProperty("_EnvironmentReflections"))
            m.SetFloat("_EnvironmentReflections", 0.85f); // no las elimina, las atenúa

        // Controla brillo general (sin eliminarlo)
        if (m.HasProperty("_Smoothness"))
        {
            float sm = m.GetFloat("_Smoothness");
            if (sm > 0.8f) m.SetFloat("_Smoothness", 0.65f); // evita reflejo tipo “cielo cromado”
        }

        // Mantén el Metallic original si existe mapa
        // (solo reduce si el valor era un default alto sin mapa)
        if (m.HasProperty("_MetallicGlossMap"))
        {
            var tex = m.GetTexture("_MetallicGlossMap");
            if (tex == null && m.HasProperty("_Metallic"))
            {
                float met = m.GetFloat("_Metallic");
                if (met > 0.9f) m.SetFloat("_Metallic", 0.5f); // modera
            }
        }

        // Doble cara (útil para paredes o pisos delgados)
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", 0f);
        if (m.HasProperty("_DoubleSidedEnable")) m.SetFloat("_DoubleSidedEnable", 1f);

        // Forzar modo opaco (sin transparencias accidentales)
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
    }
}
