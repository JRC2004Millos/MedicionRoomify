// RoomTextures.cs
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[Serializable]
public class TextureEntry
{
    public string id;
    public string albedo;
    public string normal;
    public string metallic;
    public string roughness;
    public float tilingX = 1f;
    public float tilingY = 1f;
}

[Serializable]
public class TextureCatalog
{
    public List<TextureEntry> textures = new List<TextureEntry>();
}

[DefaultExecutionOrder(-50)]
public class RoomTextures : MonoBehaviour
{
    [Header("Opcional: material de prueba")]
    public Material previewTarget;

    [Header("Estado")]
    [SerializeField] private string resolvedJsonPath = "";

    private readonly Dictionary<string, Material> _cache = new(StringComparer.OrdinalIgnoreCase);
    private TextureCatalog _catalog;

    private static readonly string[] EXT = { ".png", ".jpg", ".jpeg", ".tga", ".webp" };

    private readonly Dictionary<string, Material> _matCache = new(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        var baseDir = Application.persistentDataPath;
        var pathA = Path.Combine(baseDir, "textures_model.json");
        var pathB = Path.Combine(baseDir, "textures_model");
        var room = Path.Combine(baseDir, "room_data.json");

        Debug.Log($"[RoomTextures v2] pDir={baseDir}");
        Debug.Log($"[RoomTextures v2] exists(textures_model.json)={File.Exists(pathA)} size={(File.Exists(pathA) ? new FileInfo(pathA).Length : 0)}");
        Debug.Log($"[RoomTextures v2] exists(textures_model)={File.Exists(pathB)} size={(File.Exists(pathB) ? new FileInfo(pathB).Length : 0)}");
        Debug.Log($"[RoomTextures v2] exists(room_data.json)={File.Exists(room)} size={(File.Exists(room) ? new FileInfo(room).Length : 0)}");

        if (File.Exists(pathA)) { resolvedJsonPath = pathA; return; }
        if (File.Exists(pathB)) { resolvedJsonPath = pathB; return; }
        resolvedJsonPath = room;
    }

    private void Start() => StartCoroutine(LoadCatalogAndWarmup());

    private System.Collections.IEnumerator LoadCatalogAndWarmup()
    {
        if (!File.Exists(resolvedJsonPath))
        {
            Debug.LogWarning($"[RoomTextures] No se encontró el JSON de texturas en: {resolvedJsonPath}");
            yield break;
        }

        try
        {
            string json = File.ReadAllText(resolvedJsonPath);
            _catalog = ParseCatalog(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomTextures] Error parseando JSON: {e}");
            yield break;
        }

        if (_catalog == null || _catalog.textures == null || _catalog.textures.Count == 0)
        {
            Debug.LogWarning("[RoomTextures] Catálogo vacío (estructura no compatible o archivos PBR no encontrados).");
            yield break;
        }

        Debug.Log($"[RoomTextures] Catálogo listo. Entradas: {_catalog.textures.Count}");
        ApplyPreview(_catalog.textures[0]);
    }

    // ----------------------------------------------------------------------

    [Serializable] class TextureAssignmentItem { public string wall; public string pack; public string path; }
    [Serializable] class TextureAssignmentModel { public string project; public List<TextureAssignmentItem> items; }

    private TextureCatalog ParseCatalog(string json)
    {
        // 1) Intento estándar {"textures":[...]}
        var cat = JsonUtility.FromJson<TextureCatalog>(json);
        if (cat != null && cat.textures != null && cat.textures.Count > 0) return cat;

        // 2) Formato de Roomify {"project":"Roomify","items":[...]}
        var model = JsonUtility.FromJson<TextureAssignmentModel>(json);
        if (model?.items != null && model.items.Count > 0)
        {
            var list = new List<TextureEntry>(model.items.Count);

            foreach (var it in model.items)
            {
                if (string.IsNullOrEmpty(it?.path) || !Directory.Exists(it.path))
                {
                    Debug.LogWarning($"[RoomTextures] Carpeta PBR inexistente: {it?.path}");
                    continue;
                }

                var albedoPath = FindMap(it.path, new[] { "color", "albedo", "basecolor", "base_color" });
                var normalGL = FindMap(it.path, new[] { "normalgl" });
                var normalDX = normalGL == null ? FindMap(it.path, new[] { "normaldx", "normal_dx" }) : null;
                bool normalIsDX = normalGL == null && normalDX != null;
                var normalPath = normalGL ?? normalDX;
                var roughPath = FindMap(it.path, new[] { "roughness", "rough" });

                if (albedoPath == null)
                {
                    DumpDir(it.path, $"Contenido pack sin albedo ({it.pack})");
                    Debug.LogWarning($"[RoomTextures] Sin albedo en pack: {it.path}");
                    continue;
                }

                list.Add(new TextureEntry
                {
                    id = MakeId(it.wall, it.pack),
                    albedo = albedoPath,
                    normal = normalPath,
                    metallic = null,
                    roughness = roughPath,
                    tilingX = 1f,
                    tilingY = 1f
                });
            }

            return new TextureCatalog { textures = list };
        }

        // 3) Si la raíz es array, envolver
        var t = json.TrimStart();
        if (t.StartsWith("["))
        {
            var wrapped = "{\"textures\":" + json + "}";
            var cat2 = JsonUtility.FromJson<TextureCatalog>(wrapped);
            if (cat2?.textures != null && cat2.textures.Count > 0) return cat2;
        }

        return null;
    }

    private static string MakeId(string wall, string pack)
    {
        if (string.IsNullOrEmpty(wall)) wall = "Unknown";
        string id;
        var lower = wall.ToLowerInvariant();
        if (lower.Contains("piso")) id = "FLOOR";
        else if (lower.Contains("techo")) id = "CEILING";
        else if (lower.Contains("(east)")) id = "WALL_east";
        else if (lower.Contains("(west)")) id = "WALL_west";
        else if (lower.Contains("(north)")) id = "WALL_north";
        else if (lower.Contains("(south)")) id = "WALL_south";
        else id = wall.Replace(" ", "_");
        if (!string.IsNullOrEmpty(pack)) id += $"__{pack}";
        return id;
    }

    private string FindMap(string dir, string[] keywordsAny)
    {
        foreach (var f in Directory.GetFiles(dir))
        {
            var name = Path.GetFileName(f).ToLowerInvariant();
            var ext = Path.GetExtension(name);
            if (Array.IndexOf(EXT, ext) < 0) continue;
            foreach (var kw in keywordsAny)
                if (name.Contains(kw)) return f;
        }
        return null;
    }

    private void DumpDir(string path, string label)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Debug.LogWarning($"[RoomTextures] {label}: dir no existe -> {path}");
                return;
            }
            var files = Directory.GetFiles(path);
            Debug.Log($"[RoomTextures] {label}: {path} ({files.Length} archivos)");
            foreach (var f in files)
                Debug.Log($"[RoomTextures]   - {Path.GetFileName(f)}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RoomTextures] DumpDir error {label}: {e.Message}");
        }
    }

    private void ApplyPreview(TextureEntry e)
    {
        if (previewTarget == null || e == null) return;

        var albedo = LoadTexSRGB(e.albedo);
        if (albedo) previewTarget.SetTexture("_BaseMap", albedo);

        // Normal: si viene NormalDX, invertimos el canal G para OpenGL
        var normal = LoadTexLinear(e.normal);
        if (normal)
        {
            if (e.normal != null && e.normal.ToLowerInvariant().Contains("normaldx"))
                InvertGreen(normal);
            normal.Apply(true, false);
            previewTarget.EnableKeyword("_NORMALMAP");
            previewTarget.SetTexture("_BumpMap", normal);
        }

        // Roughness simple (opcional): como URP usa Smoothness, ponemos un valor medio
        previewTarget.SetFloat("_Smoothness", 0.6f);
    }

    private Texture2D LoadTexSRGB(string path) => string.IsNullOrEmpty(path) ? null : LoadTex(path, true);
    private Texture2D LoadTexLinear(string path) => string.IsNullOrEmpty(path) ? null : LoadTex(path, false);

    // Carga genérica desde disco
    private Texture2D LoadTex(string path, bool sRGB)
    {
        try
        {
            var data = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, !sRGB);
            tex.LoadImage(data, false);
            tex.Apply(true, false);
            return tex;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RoomTextures] No se pudo cargar {path}: {ex.Message}");
            return null;
        }
    }

    private void InvertGreen(Texture2D tex)
    {
        var pixels = tex.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            c.g = (byte)(255 - c.g);
            pixels[i] = c;
        }
        tex.SetPixels32(pixels);
    }

    private Material BuildMaterial(TextureEntry e)
    {
        if (e == null) return null;
        if (_matCache.TryGetValue(e.id, out var cached)) return cached;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        var albedo = LoadTexSRGB(e.albedo);
        if (albedo) mat.SetTexture("_BaseMap", albedo);

        var normal = LoadTexLinear(e.normal);
        if (normal)
        {
            if (e.normal != null && e.normal.ToLowerInvariant().Contains("normaldx"))
                InvertGreen(normal);
            normal.Apply(true, false);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_BumpMap", normal);
        }

        // Roughness simple -> Smoothness intermedio (URP usa Smoothness)
        mat.SetFloat("_Smoothness", 0.6f);

        _matCache[e.id] = mat;
        return mat;
    }

    public void ApplyToSceneNow() => StartCoroutine(ApplyAfterBuild());

    private System.Collections.IEnumerator ApplyAfterBuild()
    {
        // Espera a que RoomBuilder cree objetos
        for (int i = 0; i < 60; i++) // ~1s a 60 FPS
        {
            if (GameObject.Find("Floor") != null) break;
            yield return null;
        }

        if (_catalog == null || _catalog.textures == null || _catalog.textures.Count == 0)
            yield break;

        // 1) Piso
        var floorEntry = _catalog.textures
            .FirstOrDefault(t => t.id != null && t.id.ToUpperInvariant().Contains("FLOOR"));
        var floorGo = GameObject.Find("Floor");
        if (floorGo && floorEntry != null)
        {
            var mr = floorGo.GetComponent<MeshRenderer>();
            var m = BuildMaterial(floorEntry);
            if (mr && m) mr.material = m;
        }

        // 2) Muros (si tienes IDs tipo WALL_north/east/etc, intenta emparejar;
        // si no, usa el primer WALL_* para todos como fallback)
        var wallEntries = _catalog.textures
            .Where(t => t.id != null && t.id.ToUpperInvariant().Contains("WALL"))
            .ToList();

        var defaultWall = wallEntries.FirstOrDefault();

        foreach (var wall in GameObject.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!wall.name.StartsWith("Wall_")) continue;

            // Heurística por nombre si tu JSON codifica orientación:
            TextureEntry chosen = defaultWall;

            // (opcional) Si decides nombrar muros con sufijos, puedes intentar:
            // if (wall.name.Contains("(north)", StringComparison.OrdinalIgnoreCase))
            //     chosen = wallEntries.FirstOrDefault(t => t.id.Contains("WALL_north", StringComparison.OrdinalIgnoreCase)) ?? defaultWall;

            if (chosen != null)
            {
                var m = BuildMaterial(chosen);
                if (m) wall.material = m;
            }
        }
    }
}
