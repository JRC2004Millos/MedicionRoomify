// Assets/Scripts/RoomBuilder.cs
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

    void Start()
    {
        // 1) Ruta del archivo
        string path = GetJsonPath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogError($"[RoomBuilder] No se encontró el JSON: {path}");
            return;
        }

        // 2) Leer + Parsear
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

        // 3) Construir geometría
        var cornersById = data.corners.ToDictionary(c => c.id, c => c);
        var floorVerts2D = OrderPolygonByWalls(data.walls, cornersById);
        if (floorVerts2D == null || floorVerts2D.Count < 3)
        {
            Debug.LogError("[RoomBuilder] No se pudo derivar el polígono del piso.");
            return;
        }

        BuildFloor(floorVerts2D);
        BuildWalls(data.walls, cornersById, data.room_dimensions != null ? data.room_dimensions.height : 2.5f);

        // 4) Ajustar cámara
        FrameCameraToBounds(floorVerts2D);
        Debug.Log("[RoomBuilder] Renderización completada.");
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
        var verts3D = verts2D.Select(v => new Vector3(v.x, 0f, v.y)).ToArray();
        var tris = new List<int>();
        for (int i = 1; i < verts3D.Length - 1; i++)
        {
            tris.Add(0); tris.Add(i + 1); tris.Add(i);
        }

        var mesh = new Mesh();
        mesh.name = "FloorMesh";
        mesh.vertices = verts3D;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();

        var go = new GameObject("Floor", typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().mesh = mesh;

        var mr = go.GetComponent<MeshRenderer>();
        var textures = FindFirstObjectByType<RoomTextures>();
        if (textures != null)
            textures.ApplyOrQueueRenderer(mr, RoomTextures.SurfaceKind.Floor, Vector3.forward, floorMaterial);
        else
            mr.material = floorMaterial;
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
            wall.transform.position = mid + Vector3.up * (height * 0.5f);
            wall.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            wall.transform.localScale = new Vector3(wallThickness, height, len);

            var mr = wall.GetComponent<MeshRenderer>();
            var textures = FindFirstObjectByType<RoomTextures>();
            if (textures != null)
                textures.ApplyOrQueueRenderer(mr, RoomTextures.SurfaceKind.Wall, dir, wallMaterial);
            else
                mr.material = wallMaterial;
        }
    }

    void FrameCameraToBounds(List<Vector2> verts2D)
    {
        var center = Vector2.zero;
        foreach (var v in verts2D) center += v;
        center /= verts2D.Count;
        float maxR = verts2D.Max(v => Vector2.Distance(center, v));

        var cam = Camera.main;
        if (cam == null) return;
        Vector3 c3 = new Vector3(center.x, 0, center.y);
        cam.transform.position = c3 + new Vector3(0, maxR * 2.0f, maxR * 1.6f);
        cam.transform.LookAt(c3 + Vector3.up * 0.5f, Vector3.up);
    }
}
