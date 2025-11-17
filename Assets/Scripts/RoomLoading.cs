using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class RoomLoading : MonoBehaviour
{
    [Header("Ruta de layout (opcional)")]
    public string layoutOverridePath = "";

    [Header("Superficies presentes en la escena")]
    public List<SurfaceBinding> sceneSurfaces = new List<SurfaceBinding>();

    private Dictionary<string, SurfaceBinding> _surfaceMap;
    [Header("Opciones de geometría (reconstrucción)")]
    public float wallThickness = 0.1f;
    public Material defaultFloorMaterial;
    public Material defaultWallMaterial;
    public RoomSpace roomSpace;

    private float floorBaseY = 0.05f;

    private readonly Dictionary<string, Material> _matCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] TEX_EXT = { ".png", ".jpg", ".jpeg", ".tga", ".webp" };
    private int _loadedSinceLastFlush = 0;
    const int MAX_TEX_SIZE = 4096;
    const int FLUSH_EVERY  = 8;

    [Header("Modelos desde Resources/Catalog")]
    [SerializeField] private string catalogBasePath = "Catalog";
    private Dictionary<string, GameObject> _prefabCacheByName = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] CatalogFolders =
    {
        "",
        "Monitor",
        "Mouse",
        "Keyboard",
        "Plant",
        "Sofa",
        "Lamp",
        "Table",
        "chair",
        "chairs",
        "sofas",
        "mesas",
        "screens",
        "mouse",
        "keyboards",
        "pc",
        "lamparas",
        "furniture"
    };

    private void Awake()
    {
        BuildSurfaceMap();
    }

    private void Start()
    {
        string path = ResolveLayoutPath();

        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[RoomLoading] No se proporcionó ruta de layout.");
            return;
        }

        if (!File.Exists(path))
        {
            Debug.LogError("[RoomLoading] El archivo de layout no existe: " + path);
            return;
        }

        Debug.Log("[RoomLoading] Cargando layout desde: " + path);

        string json = File.ReadAllText(path);
        FinalRoomModel data = null;
        try
        {
            data = JsonUtility.FromJson<FinalRoomModel>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("[RoomLoading] Error al parsear FinalRoomModel: " + e.Message);
            return;
        }

        if (data == null)
        {
            Debug.LogError("[RoomLoading] FinalRoomModel nulo al deserializar JSON.");
            return;
        }

        Debug.Log($"[RoomLoading] Layout cargado. roomId={data.roomId}, spaceName={data.spaceName}");

        BuildGeometryFromFinalModel(data);
        BuildSurfaceMap();
        ApplyTextures(data);
        RebuildFurniture(data);
    }

    private string ResolveLayoutPath()
    {
        if (!string.IsNullOrEmpty(layoutOverridePath))
        {
            return layoutOverridePath;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var intent   = activity.Call<AndroidJavaObject>("getIntent");
                string extra = intent.Call<string>("getStringExtra", "ROOM_LAYOUT_PATH");

                if (!string.IsNullOrEmpty(extra))
                {
                    Debug.Log("[RoomLoading] ROOM_LAYOUT_PATH desde Intent: " + extra);
                    return extra;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[RoomLoading] Error leyendo ROOM_LAYOUT_PATH del Intent: " + e.Message);
        }

        return null;
#else
        return layoutOverridePath;
#endif
    }

    private void BuildSurfaceMap()
    {
        if (_surfaceMap == null)
            _surfaceMap = new Dictionary<string, SurfaceBinding>(StringComparer.OrdinalIgnoreCase);
        else
            _surfaceMap.Clear();

        foreach (var s in sceneSurfaces)
        {
            if (s == null || string.IsNullOrEmpty(s.surfaceId) || s.renderer == null)
                continue;

            var key = NormalizeSurfaceKey(s.surfaceId);

            if (!_surfaceMap.ContainsKey(key))
            {
                _surfaceMap.Add(key, s);
            }
            else
            {
                Debug.LogWarning("[RoomLoading] surfaceId duplicado en sceneSurfaces (normalizado): " + s.surfaceId);
            }
        }

        foreach (var k in _surfaceMap.Keys)
        {
            Debug.Log("[RoomLoading] SurfaceMap key: " + k);
        }
    }

    private GameObject GetFurniturePrefab(string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
            return null;

        prefabName = prefabName.Trim();

        if (_prefabCacheByName != null && _prefabCacheByName.TryGetValue(prefabName, out var cached))
            return cached;

        GameObject found = null;

        foreach (var folder in CatalogFolders)
        {
            string path;
            if (string.IsNullOrEmpty(folder))
                path = $"{catalogBasePath}/{prefabName}";
            else
                path = $"{catalogBasePath}/{folder}/{prefabName}";

            var go = Resources.Load<GameObject>(path);
            if (go != null)
            {
                found = go;
                Debug.Log($"[RoomLoading] Cargado modelo '{prefabName}' desde Resources/{path}");
                break;
            }
        }

        if (found == null)
        {
            var direct = Resources.Load<GameObject>(prefabName);
            if (direct != null)
            {
                found = direct;
                Debug.Log($"[RoomLoading] Cargado modelo '{prefabName}' vía Resources.Load('{prefabName}')");
            }
        }

        if (found != null)
        {
            _prefabCacheByName[prefabName] = found;
            return found;
        }

        Debug.LogWarning($"[RoomLoading] No se encontró modelo para nombre: {prefabName}");
        return null;
    }

    private SurfaceBinding GetSurfaceBinding(string surfaceId)
    {
        if (string.IsNullOrEmpty(surfaceId)) return null;

        if (_surfaceMap == null)
            return null;

        var key = NormalizeSurfaceKey(surfaceId);

        if (_surfaceMap.TryGetValue(key, out var sb))
            return sb;

        Debug.LogWarning("[RoomLoading] No se encontró SurfaceBinding para surfaceId (normalizado): " + surfaceId);
        return null;
    }

    private void ApplyTextures(FinalRoomModel data)
    {
        if (data.textures == null || data.textures.Count == 0)
        {
            Debug.Log("[RoomLoading] No hay texturas en el JSON para aplicar.");
            return;
        }

        foreach (var t in data.textures)
        {
            if (t == null || string.IsNullOrEmpty(t.wall)) continue;

            var binding = GetSurfaceBinding(t.wall);
            if (binding == null || binding.renderer == null)
                continue;

            ApplyPbrPackToRenderer(binding.renderer, t.path, t.pack, t.wall);
        }
    }

    private void ApplyPbrPackToRenderer(Renderer renderer, string packPath, string packName, string wallName)
    {
        if (renderer == null)
            return;

        string texId = MakeTexId(wallName, packName);

        if (!_matCache.TryGetValue(texId, out var mat))
        {
            string dir = NormalizePbrDir(packPath);
            if (string.IsNullOrEmpty(dir))
            {
                Debug.LogWarning($"[RoomLoading] No se encontró carpeta PBR para pack '{packName}' en path '{packPath}'");
                return;
            }

            var albedo = FindMap(dir, new[] { "color", "albedo", "basecolor", "_col" });
            var normal = FindMap(dir, new[] { "normalgl", "normaldx" });
            var rough  = FindMap(dir, new[] { "roughness", "rough" });

            if (albedo == null)
            {
                Debug.LogWarning($"[RoomLoading] No se encontró mapa de albedo para pack '{packName}' en '{dir}'");
                return;
            }

            mat = BuildMaterial(texId, albedo, normal, rough, 1f, 1f);
            if (mat == null) return;
        }

        renderer.material = mat;
        Debug.Log($"[RoomLoading] Aplicado material '{mat.name}' a '{renderer.gameObject.name}'");
    }

    private string MakeTexId(string wall, string pack)
    {
        if (string.IsNullOrEmpty(wall)) wall = "Unknown";
        var lower = wall.ToLowerInvariant();
        string id = lower.Contains("piso") ? "FLOOR"
                 : lower.Contains("techo") ? "CEILING"
                 : lower.Contains("east") ? "WALL_east"
                 : lower.Contains("west") ? "WALL_west"
                 : lower.Contains("north") ? "WALL_north"
                 : lower.Contains("south") ? "WALL_south" : "WALL_";
        if (!string.IsNullOrEmpty(pack)) id += $"__{pack}";
        return id;
    }

    private string NormalizePbrDir(string jsonDir)
    {
        if (string.IsNullOrEmpty(jsonDir)) return null;
        if (Directory.Exists(jsonDir)) return jsonDir;

        try
        {
            var basePath = Application.persistentDataPath;
            var idx = jsonDir.IndexOf("/files", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var after = jsonDir.Substring(idx + "/files".Length).TrimStart('/');
                var candidate = Path.Combine(basePath, after).Replace("\\", "/");
                if (Directory.Exists(candidate)) return candidate;
            }

            var pack = Path.GetFileName(jsonDir.TrimEnd('/', '\\'));
            var pbr = Path.Combine(basePath, "pbrpacks", pack);
            if (Directory.Exists(pbr)) return pbr;
        }
        catch { }
        return null;
    }

    private string FindMap(string dir, string[] keys)
    {
        if (!Directory.Exists(dir)) return null;
        foreach (var f in Directory.GetFiles(dir))
        {
            var name = Path.GetFileName(f).ToLowerInvariant();
            var ext = Path.GetExtension(name);
            if (!TEX_EXT.Contains(ext)) continue;
            if (keys.Any(k => name.Contains(k))) return f;
        }
        return null;
    }

    private Material BuildMaterial(string id, string albedoPath, string normalPath, string roughPath, float tilingX, float tilingY)
    {
        if (_matCache.TryGetValue(id, out var cached)) return cached;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = id;

        var albedo = LoadTex(albedoPath, true);
        if (albedo)
        {
            mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTextureScale("_BaseMap", new Vector2(tilingX, tilingY));
        }

        var normal = LoadTex(normalPath, false);
        if (normal)
        {
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1f);
            mat.SetTextureScale("_BumpMap", new Vector2(tilingX, tilingY));
        }

        mat.SetFloat("_Metallic", 0.0f);
        mat.SetFloat("_Smoothness", 0.35f);

        _matCache[id] = mat;
        return mat;
    }

    private Texture2D LoadTex(string path, bool sRGB)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

        try
        {
            var data = File.ReadAllBytes(path);

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, !sRGB);
            if (!ImageConversion.LoadImage(tex, data, markNonReadable: false))
                throw new Exception("LoadImage devolvió false");

            tex.name = Path.GetFileName(path);
            int w0 = tex.width, h0 = tex.height;

            tex = DownscaleIfNeeded(tex, MAX_TEX_SIZE, sRGB);

            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;

            tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);

            Debug.Log($"[RoomLoading] tex OK {tex.name} {w0}x{h0} -> {tex.width}x{tex.height}");
            MaybeFlush();
            return tex;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RoomLoading] fallo LoadTex {Path.GetFileName(path)}: {e.Message}");
            return null;
        }
    }

    private Texture2D DownscaleIfNeeded(Texture2D src, int maxSize, bool sRGB)
    {
        if (src == null) return null;
        int w = src.width, h = src.height;
        if (w <= maxSize && h <= maxSize) return src;

        float k = (float)maxSize / Mathf.Max(w, h);
        int nw = Mathf.Max(2, Mathf.RoundToInt(w * k));
        int nh = Mathf.Max(2, Mathf.RoundToInt(h * k));

        var rt = new RenderTexture(nw, nh, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
        {
            useMipMap = true,
            autoGenerateMips = true
        };

        var prev = RenderTexture.active;
        try
        {
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;

            var dst = new Texture2D(nw, nh, TextureFormat.RGBA32, true, !sRGB);
            dst.ReadPixels(new Rect(0, 0, nw, nh), 0, 0);
            dst.Apply(updateMipmaps: true, makeNoLongerReadable: false);

            Destroy(src);
            return dst;
        }
        finally
        {
            RenderTexture.active = prev;
            rt.Release();
            Destroy(rt);
        }
    }

    private void MaybeFlush()
    {
        _loadedSinceLastFlush++;
        if (_loadedSinceLastFlush >= FLUSH_EVERY)
        {
            _loadedSinceLastFlush = 0;
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }
    }

    private void RebuildFurniture(FinalRoomModel data)
    {
        ClearExistingFurniture();

        if (data.items == null || data.items.Count == 0)
        {
            Debug.Log("[RoomLoading] No hay muebles en el JSON.");
            return;
        }

        foreach (var item in data.items)
        {
            if (item == null || string.IsNullOrEmpty(item.prefabName))
                continue;

            var prefab = GetFurniturePrefab(item.prefabName);
            if (prefab == null)
                continue;

            var instance = Instantiate(prefab, item.position, item.rotation);
            instance.transform.localScale = item.scale;
            instance.tag = "Furniture";
        }
    }

    private void ClearExistingFurniture()
    {
        var existing = GameObject.FindGameObjectsWithTag("Furniture");
        foreach (var go in existing)
        {
            Destroy(go);
        }
    }

    private void BuildGeometryFromFinalModel(FinalRoomModel model)
    {
        if (model == null || model.geometry == null)
        {
            Debug.LogError("[RoomLoading] Geometry nula en FinalRoomModel.");
            return;
        }

        sceneSurfaces.Clear();
        var geo = model.geometry;

        if (geo.corners == null || geo.walls == null || geo.corners.Count < 3)
        {
            Debug.LogError("[RoomLoading] Geometry inválida (corners/walls).");
            return;
        }

        float roomHeight = (geo.room_dimensions != null && geo.room_dimensions.height > 0f)
            ? geo.room_dimensions.height
            : 2.5f;

        var cornersById = new Dictionary<string, Corner>();
        foreach (var c in geo.corners)
        {
            if (c == null || string.IsNullOrEmpty(c.id)) continue;
            if (!cornersById.ContainsKey(c.id))
                cornersById.Add(c.id, c);
        }

        var floorVerts2D = OrderPolygonByWalls(geo.walls, cornersById);
        if (floorVerts2D == null || floorVerts2D.Count < 3)
        {
            Debug.LogError("[RoomLoading] No se pudo derivar el polígono del piso desde geometry.");
            return;
        }

        var floorRenderer = BuildFloorMesh(floorVerts2D);
        sceneSurfaces.Add(new SurfaceBinding
        {
            surfaceId = "Piso",
            renderer = floorRenderer
        });

        foreach (var w in geo.walls)
        {
            if (w == null) continue;
            if (!cornersById.TryGetValue(w.from, out var ca)) continue;
            if (!cornersById.TryGetValue(w.to, out var cb)) continue;

            var wallRenderer = BuildWallCube(ca, cb, roomHeight);

            string dir = w.direction;
            string surfaceId = $"Pared de {w.from} a {w.to} ({dir})";

            sceneSurfaces.Add(new SurfaceBinding
            {
                surfaceId = surfaceId,
                renderer = wallRenderer
            });
        }

        if (roomSpace != null)
        {
            roomSpace.SetFloorPolygonWorldXZ(floorVerts2D);

            roomSpace.minX = roomSpace.maxX = floorVerts2D[0].x;
            roomSpace.minZ = roomSpace.maxZ = floorVerts2D[0].y;

            foreach (var v in floorVerts2D)
            {
                if (v.x < roomSpace.minX) roomSpace.minX = v.x;
                if (v.x > roomSpace.maxX) roomSpace.maxX = v.x;
                if (v.y < roomSpace.minZ) roomSpace.minZ = v.y;
                if (v.y > roomSpace.maxZ) roomSpace.maxZ = v.y;
            }
        }

        var ceilingRenderer = BuildCeilingMesh(floorVerts2D, roomHeight);
        sceneSurfaces.Add(new SurfaceBinding
        {
            surfaceId = "Techo",
            renderer = ceilingRenderer
        });
    }

    private List<Vector2> OrderPolygonByWalls(IList<Wall> walls, Dictionary<string, Corner> cornersById)
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

            Wall next = null;
            bool found = false;
            foreach (var w in walls)
            {
                if (w.from == cur)
                {
                    next = w;
                    found = true;
                    break;
                }
            }
            if (!found || next == null) break;

            cur = next.to;
            if (cur == start) break;
        }

        return verts;
    }

    private MeshRenderer BuildFloorMesh(List<Vector2> verts2D)
    {
        if (verts2D == null || verts2D.Count < 3) return null;

        if (SignedArea2D(verts2D) > 0f)
            verts2D.Reverse();

        var verts3D = new Vector3[verts2D.Count];
        for (int i = 0; i < verts2D.Count; i++)
            verts3D[i] = new Vector3(verts2D[i].x, floorBaseY, verts2D[i].y);

        var tris = new List<int>();
        for (int i = 1; i < verts3D.Length - 1; i++)
        {
            tris.Add(0); tris.Add(i); tris.Add(i + 1);
        }

        float minX = verts2D[0].x, maxX = verts2D[0].x;
        float minZ = verts2D[0].y, maxZ = verts2D[0].y;
        foreach (var v in verts2D)
        {
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
            if (v.y < minZ) minZ = v.y;
            if (v.y > maxZ) maxZ = v.y;
        }

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
        mesh.indexFormat = verts3D.Length > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        mesh.vertices  = verts3D;
        mesh.triangles = tris.ToArray();
        mesh.uv        = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        var go = new GameObject("Floor", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        go.transform.SetParent(this.transform, true);

        var mf = go.GetComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>();
        var mc = go.GetComponent<MeshCollider>();

        mf.sharedMesh = mesh;
        mc.sharedMesh = mesh;
        mc.convex = false;

        if (defaultFloorMaterial == null)
        {
            defaultFloorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            defaultFloorMaterial.color = new Color(0.6f, 0.6f, 0.6f);
        }

        mr.material = defaultFloorMaterial;

        return mr;
    }

    private float SignedArea2D(List<Vector2> poly)
    {
        double a = 0;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            a += (double)(poly[j].x * poly[i].y - poly[i].x * poly[j].y);
        return (float)(0.5 * a);
    }

    private MeshRenderer BuildWallCube(Corner a, Corner b, float height)
    {
        var a3 = new Vector3(a.position.x, 0f, a.position.y);
        var b3 = new Vector3(b.position.x, 0f, b.position.y);

        Vector3 mid = (a3 + b3) * 0.5f;
        Vector3 dir = (b3 - a3).normalized;
        float len   = Vector3.Distance(a3, b3);

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = $"Wall_{a.id}_{b.id}";
        wall.layer = LayerMask.NameToLayer("RoomWall");

        wall.transform.SetParent(this.transform, true);
        wall.transform.position = mid + Vector3.up * (floorBaseY + height * 0.5f);
        wall.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        wall.transform.localScale = new Vector3(wallThickness, height, len);

        var mr = wall.GetComponent<MeshRenderer>();

        if (defaultWallMaterial == null)
        {
            defaultWallMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            defaultWallMaterial.color = Color.white;
        }

        mr.material = defaultWallMaterial;
        return mr;
    }

    private void FrameCameraToBounds(List<Vector2> floorVerts2D)
    {
        var cam = Camera.main;
        if (cam == null || floorVerts2D == null || floorVerts2D.Count == 0)
            return;

        float minX = floorVerts2D[0].x, maxX = floorVerts2D[0].x;
        float minZ = floorVerts2D[0].y, maxZ = floorVerts2D[0].y;

        foreach (var v in floorVerts2D)
        {
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
            if (v.y < minZ) minZ = v.y;
            if (v.y > maxZ) maxZ = v.y;
        }

        Vector3 center = new Vector3((minX + maxX) * 0.5f, floorBaseY, (minZ + maxZ) * 0.5f);
        float sizeX = maxX - minX;
        float sizeZ = maxZ - minZ;
        float maxSize = Mathf.Max(sizeX, sizeZ);

        float height = Mathf.Max(3f, maxSize * 1.5f);
        cam.transform.position = center + Vector3.up * height;
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        if (cam.orthographic)
        {
            cam.orthographicSize = maxSize;
        }
    }

    private MeshRenderer BuildCeilingMesh(List<Vector2> verts2D, float roomHeight)
    {
        if (verts2D == null || verts2D.Count < 3) return null;

        var verts2DCopy = new List<Vector2>(verts2D);
        if (SignedArea2D(verts2DCopy) < 0f)
            verts2DCopy.Reverse();

        var verts3D = new Vector3[verts2DCopy.Count];
        float ceilingY = floorBaseY + roomHeight;

        for (int i = 0; i < verts2DCopy.Count; i++)
            verts3D[i] = new Vector3(verts2DCopy[i].x, ceilingY, verts2DCopy[i].y);

        var tris = new List<int>();
        for (int i = 1; i < verts3D.Length - 1; i++)
        {
            tris.Add(0); tris.Add(i + 1); tris.Add(i);
        }

        float minX = verts2DCopy[0].x, maxX = verts2DCopy[0].x;
        float minZ = verts2DCopy[0].y, maxZ = verts2DCopy[0].y;
        foreach (var v in verts2DCopy)
        {
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
            if (v.y < minZ) minZ = v.y;
            if (v.y > maxZ) maxZ = v.y;
        }

        float spanX = Mathf.Max(0.001f, maxX - minX);
        float spanZ = Mathf.Max(0.001f, maxZ - minZ);

        var uvs = new Vector2[verts3D.Length];
        for (int i = 0; i < verts3D.Length; i++)
        {
            float u = (verts3D[i].x - minX) / spanX;
            float v = (verts3D[i].z - minZ) / spanZ;
            uvs[i] = new Vector2(u, v);
        }

        var mesh = new Mesh { name = "CeilingMesh" };
        mesh.indexFormat = verts3D.Length > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        mesh.vertices  = verts3D;
        mesh.triangles = tris.ToArray();
        mesh.uv        = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        var go = new GameObject("Ceiling", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        go.transform.SetParent(this.transform, true);

        var mf = go.GetComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>();
        var mc = go.GetComponent<MeshCollider>();

        mf.sharedMesh = mesh;
        mc.sharedMesh = mesh;
        mc.convex = false;

        if (defaultWallMaterial == null)
        {
            defaultWallMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            defaultWallMaterial.color = Color.white;
        }

        mr.material = defaultWallMaterial;

        return mr;
    }

    private string NormalizeSurfaceKey(string id)
    {
        if (string.IsNullOrEmpty(id))
            return string.Empty;

        id = id.Trim();

        while (id.Contains("  "))
            id = id.Replace("  ", " ");

        int paren = id.IndexOf('(');
        if (paren >= 0)
        {
            id = id.Substring(0, paren).Trim();
        }

        return id.ToLowerInvariant();
    }
}

[Serializable]
public class SurfaceBinding
{
    public string surfaceId;
    public Renderer renderer;
}

[Serializable]
public class FurniturePrefabEntry
{
    public string prefabName;
    public GameObject prefab;
}