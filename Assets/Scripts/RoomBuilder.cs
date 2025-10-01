// Assets/Scripts/RoomBuilder.cs
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class RoomBuilder : MonoBehaviour
{
    [Header("Editor only (pruebas)")]
    [Tooltip("Ruta absoluta al JSON cuando pruebas en el Editor.")]
    public string editorJsonPath;

    [Header("Opciones de render")]
    [Tooltip("Espesor de los muros en metros.")]
    public float wallThickness = 0.05f;
    [Tooltip("Material para el piso (opcional).")]
    public Material floorMaterial;
    [Tooltip("Material para los muros (opcional).")]
    public Material wallMaterial;

    private RoomData data;

    void Start()
    {
        // 1) Ruta del archivo
        string path = GetJsonPath();
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[RoomBuilder] Ruta del JSON vacía. Configura editorJsonPath o pasa jsonRoomPath por Intent.");
            return;
        }
        if (!File.Exists(path))
        {
            Debug.LogError($"[RoomBuilder] No se encontró el archivo JSON en: {path}");
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
            Debug.LogError("[RoomBuilder] JSON inválido o incompleto (corners/walls).");
            return;
        }

        if (floorMaterial == null)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); // o "Standard" si Built-in
            m.color = new Color(0.5f, 0.5f, 0.5f);
            floorMaterial = m;
        }
        if (wallMaterial == null)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); // o "Standard"
            m.color = Color.white;
            wallMaterial = m;
        }


        // 3) Construir escena
        var cornersById = data.corners.ToDictionary(c => c.id, c => c);
        var floorVerts2D = OrderPolygonByWalls(data.walls, cornersById);
        if (floorVerts2D == null || floorVerts2D.Count < 3)
        {
            Debug.LogError("[RoomBuilder] No se pudo derivar el polígono del piso.");
            return;
        }

        BuildFloor(floorVerts2D);
        BuildWalls(data.walls, cornersById, data.room_dimensions != null ? data.room_dimensions.height : 2.5f);

        // 4) Ajustar cámara (simple)
        FrameCameraToBounds(floorVerts2D);
        Debug.Log("[RoomBuilder] Renderización básica completada.");
    }

    string GetJsonPath()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // En Android, persistentDataPath = /storage/emulated/0/Android/data/<package>/files
        string candidate = System.IO.Path.Combine(Application.persistentDataPath, "room_data.json");
        return candidate;
#else
        // En Editor, usa el campo para pruebas
        return editorJsonPath;
#endif
    }

    List<Vector2> OrderPolygonByWalls(IList<Wall> walls, Dictionary<string, Corner> cornersById)
    {
        // Asume que walls describe el contorno (A->B, B->C, ... , Z->A)
        var verts = new List<Vector2>();
        if (walls == null || walls.Count == 0) return verts;

        string start = walls[0].from;
        string cur = start;
        int guard = 0;

        while (guard++ < 1024)
        {
            if (!cornersById.ContainsKey(cur)) return null;
            var c = cornersById[cur];
            verts.Add(new Vector2(c.position.x, c.position.y));

            var nextEdge = walls.FirstOrDefault(w => w.from == cur);
            if (nextEdge == null) break;
            cur = nextEdge.to;
            if (cur == start) break;
        }
        return verts;
    }

    void BuildFloor(List<Vector2> verts2D)
    {
        // Triangulación simple tipo "fan" (válido para rectángulos/convexos)
        var verts3D = verts2D.Select(v => new Vector3(v.x, 0f, v.y)).ToArray();
        var triangles = new List<int>();
        for (int i = 1; i < verts3D.Length - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(i + 1);
        }

        var mesh = new Mesh();
        mesh.name = "FloorMesh";
        mesh.vertices = verts3D;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("Floor", typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().mesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        if (floorMaterial != null) mr.material = floorMaterial;
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
            Vector3 dir = (b3 - a3);
            float length = dir.magnitude;
            if (length < 1e-4f) continue;
            dir.Normalize();

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Wall_{w.from}_{w.to}";
            wall.transform.position = mid + Vector3.up * (height * 0.5f);
            wall.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            wall.transform.localScale = new Vector3(wallThickness, height, length);

            var mr = wall.GetComponent<MeshRenderer>();
            if (wallMaterial != null) mr.material = wallMaterial;
        }
    }

    void FrameCameraToBounds(List<Vector2> verts2D)
    {
        // Centro y tamaño aproximado
        var center = Vector2.zero;
        foreach (var v in verts2D) center += v;
        center /= verts2D.Count;

        float maxR = 0f;
        foreach (var v in verts2D) maxR = Mathf.Max(maxR, Vector2.Distance(center, v));

        var cam = Camera.main;
        if (cam == null) return;

        Vector3 center3 = new Vector3(center.x, 0f, center.y);
        // Coloca la cámara en una vista 3/4
        cam.transform.position = center3 + new Vector3(0, maxR * 2.0f, maxR * 1.6f);
        cam.transform.LookAt(center3 + Vector3.up * 0.5f, Vector3.up);
    }
}
