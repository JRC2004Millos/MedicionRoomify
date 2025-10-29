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
    public string roughness;
    public float tilingX = 1f;
    public float tilingY = 1f;
}

[Serializable]
public class TextureCatalog
{
    public List<TextureEntry> textures = new();
}

[DefaultExecutionOrder(-50)]
public class RoomTextures : MonoBehaviour
{
    public enum SurfaceKind { Floor, Wall, Ceiling }

    private TextureCatalog _catalog;
    private readonly Dictionary<string, Material> _matCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PendingAssign> _pending = new();
    private static readonly string[] EXT = { ".png", ".jpg", ".jpeg", ".tga", ".webp" };

    int _loadedSinceLastFlush = 0;

    private struct PendingAssign
    {
        public MeshRenderer mr;
        public SurfaceKind kind;
        public Vector3 forward;
        public Material fallback;
    }

    void Start()
    {
        string path = Path.Combine(Application.persistentDataPath, "textures_model.json");
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[RoomTextures] No se encontró textures_model.json en {path}");
            return;
        }
        try
        {
            string json = File.ReadAllText(path);
            _catalog = ParseCatalog(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomTextures] Error parseando JSON: {e}");
            return;
        }

        if (_catalog == null || _catalog.textures.Count == 0)
        {
            Debug.LogWarning("[RoomTextures] Catálogo vacío.");
            return;
        }

        Debug.Log($"[RoomTextures] Catálogo listo. Entradas: {_catalog.textures.Count}");
        FlushPending();
    }

    // =======================
    // API usada por RoomBuilder
    // =======================
    public void ApplyOrQueueRenderer(MeshRenderer mr, SurfaceKind kind, Vector3 forward, Material fallback)
    {
        if (mr == null) return;

        if (_catalog != null && _catalog.textures.Count > 0)
        {
            if (!TryApplyNow(mr, kind, forward))
            {
                if (fallback != null) mr.material = fallback;
            }
        }
        else
        {
            _pending.Add(new PendingAssign { mr = mr, kind = kind, forward = forward, fallback = fallback });
        }
    }

    private bool TryApplyNow(MeshRenderer mr, SurfaceKind kind, Vector3 forward)
    {
        TextureEntry entry = null;

        if (kind == SurfaceKind.Floor)
            entry = GetEntryByPrefix("FLOOR");
        else if (kind == SurfaceKind.Ceiling)
            entry = GetEntryByPrefix("CEILING");
        else
        {
            string card = ComputeCardinal(forward);
            entry = GetEntryByPrefix($"WALL_{card}") ?? GetEntryByPrefix("WALL_");
        }

        if (entry == null)
        {
            Debug.Log($"[RoomTextures] (sin pack) {kind} en {mr.gameObject.name}");
            return false;
        }

        var mat = BuildMaterial(entry);
        if (mat == null) return false;

        mr.material = mat;
        Debug.Log($"[RoomTextures] ✅ Aplicado {mat.name} a {mr.gameObject.name}");
        return true;
    }
    
    private void FlushPending()
    {
        if (_pending.Count == 0) return;
        int ok = 0, fb = 0;
        foreach (var p in _pending)
        {
            if (!TryApplyNow(p.mr, p.kind, p.forward))
            {
                if (p.fallback != null)
                {
                    p.mr.material = p.fallback;
                    fb++;
                }
            }
            else ok++;
        }
        _pending.Clear();
        Debug.Log($"[RoomTextures] Pendientes aplicados -> ok={ok}, fallback={fb}");
    }

    // =======================
    // Helpers
    // =======================

    [Serializable] class TextureAssignmentItem { public string wall; public string pack; public string path; }
    [Serializable] class TextureAssignmentModel { public List<TextureAssignmentItem> items; }

    private TextureCatalog ParseCatalog(string json)
    {
        var model = JsonUtility.FromJson<TextureAssignmentModel>(json);
        if (model?.items == null || model.items.Count == 0) return null;

        var list = new List<TextureEntry>();
        foreach (var it in model.items)
        {
            var dir = NormalizePbrDir(it.path);
            if (string.IsNullOrEmpty(dir)) continue;

            var albedo = FindMap(dir, new[] { "color", "albedo", "basecolor", "_col" });
            var normal = FindMap(dir, new[] { "normalgl", "normaldx" });
            var rough = FindMap(dir, new[] { "roughness", "rough" });
            if (albedo == null) continue;

            list.Add(new TextureEntry
            {
                id = MakeId(it.wall, it.pack),
                albedo = albedo,
                normal = normal,
                roughness = rough
            });
        }
        return new TextureCatalog { textures = list };
    }

    private string FindMap(string dir, string[] keys)
    {
        if (!Directory.Exists(dir)) return null;
        foreach (var f in Directory.GetFiles(dir))
        {
            var name = Path.GetFileName(f).ToLowerInvariant();
            var ext = Path.GetExtension(name);
            if (!EXT.Contains(ext)) continue;
            if (keys.Any(k => name.Contains(k))) return f;
        }
        return null;
    }

    private string MakeId(string wall, string pack)
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

    private string ComputeCardinal(Vector3 fwd)
    {
        var v = new Vector2(fwd.x, fwd.z).normalized;
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
            return v.x >= 0 ? "east" : "west";
        else
            return v.y >= 0 ? "north" : "south";
    }

    private TextureEntry GetEntryByPrefix(string prefix)
    {
        if (_catalog == null) return null;
        return _catalog.textures.FirstOrDefault(t => t.id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private Material BuildMaterial(TextureEntry e)
    {
        if (_matCache.TryGetValue(e.id, out var cached)) return cached;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = e.id;

        var albedo = LoadTex(e.albedo, true);
        if (albedo)
        {
            mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTextureScale("_BaseMap", new Vector2(e.tilingX, e.tilingY));
        }

        var normal = LoadTex(e.normal, false);
        if (normal)
        {
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", 1f);
            mat.SetTextureScale("_BumpMap", new Vector2(e.tilingX, e.tilingY));
        }

        mat.SetFloat("_Metallic", 0.0f);
        mat.SetFloat("_Smoothness", 0.35f);

        _matCache[e.id] = mat;
        return mat;
    }

    const int MAX_TEX_SIZE = 4096;
    const int FLUSH_EVERY  = 8;

    private Texture2D LoadTex(string path, bool sRGB)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

        try
        {
            var data = File.ReadAllBytes(path);

            // 1) Cargar readable
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, /*mips*/ true, /*linear*/ !sRGB);
            if (!ImageConversion.LoadImage(tex, data, markNonReadable: false))
                throw new Exception("LoadImage devolvió false");

            tex.name = Path.GetFileName(path);
            int w0 = tex.width, h0 = tex.height;

            // 2) Downscale si hace falta (sigue readable dentro de esta función)
            tex = DownscaleIfNeeded(tex, MAX_TEX_SIZE, sRGB);

            // 3) Ajustes de muestreo
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;

            // 4) Generar mips y marcar no-readable recién al final
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);

            Debug.Log($"[RoomTextures] tex OK {tex.name} {w0}x{h0} -> {tex.width}x{tex.height}");
            MaybeFlush();
            return tex;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RoomTextures] fallo LoadTex {Path.GetFileName(path)}: {e.Message}");
            return null;
        }
    }

    static Texture2D DownscaleIfNeeded(Texture2D src, int maxSize, bool sRGB)
    {
        if (src == null) return null;
        int w = src.width, h = src.height;
        if (w <= maxSize && h <= maxSize) return src;

        float k = (float)maxSize / Mathf.Max(w, h);
        int nw = Mathf.Max(2, Mathf.RoundToInt(w * k));
        int nh = Mathf.Max(2, Mathf.RoundToInt(h * k));

        // rt con mips para que Unity genere pirámide al downscale
        var rt = new RenderTexture(nw, nh, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
        {
            useMipMap = true,
            autoGenerateMips = true
        };

        // Guardar el RT activo
        var prev = RenderTexture.active;
        try
        {
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;

            var dst = new Texture2D(nw, nh, TextureFormat.RGBA32, /*mips*/ true, /*linear*/ !sRGB);
            dst.ReadPixels(new Rect(0, 0, nw, nh), 0, 0);
            // OJO: acá aún NO la marcamos no-readable; eso se hace en LoadTex -> Apply(..., true)
            dst.Apply(updateMipmaps: true, makeNoLongerReadable: false);

            UnityEngine.Object.Destroy(src); // descarta la grande
            return dst;
        }
        finally
        {
            // Restaurar SIEMPRE y limpiar sin dejar el RT activo
            RenderTexture.active = prev != null ? prev : null;
            if (RenderTexture.active == rt) RenderTexture.active = null;
            rt.Release();
            UnityEngine.Object.Destroy(rt);
        }
    }

    void MaybeFlush()
    {
        _loadedSinceLastFlush++;
        if (_loadedSinceLastFlush >= FLUSH_EVERY)
        {
            _loadedSinceLastFlush = 0;
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }
    }
}
