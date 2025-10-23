using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class TexturePack
{
    public string albedo;       // absolute path or persistentDataPath-relative
    public string normal;       // optional
    public string roughness;    // optional (not wired by default)
    public string metallic;     // optional (not wired by default)

    public float tilingX = 1f;  // UV tiling (repeat)
    public float tilingY = 1f;
    public float rotation = 0f; // degrees clockwise (optional, 0 by default)
}

[Serializable]
public class SurfaceTextureDef
{
    public string id;           // surface id — for walls we use "A-B"; for floor use "FLOOR"
    public TexturePack pack;
}

[Serializable]
public class RoomTextures
{
    public List<SurfaceTextureDef> surfaces = new List<SurfaceTextureDef>();
}

[Serializable]
public class TexturesModel
{
    public string project;
    public List<TexturesModelItem> items = new();
}

[Serializable]
public class TexturesModelItem
{
    public string wall;   // e.g., "Pared de A a B (east)" | "Piso" | "Techo"
    public string pack;   // e.g., "Plaster001_8K-PNG"
    public string path;   // absolute folder containing the PBR texture files
}

public static class TextureUtils
{
    public static Texture2D LoadTextureFromFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        string resolved = path;
        // If path is relative, try persistentDataPath
        if (!Path.IsPathRooted(resolved))
            resolved = Path.Combine(Application.persistentDataPath, path);

        if (!File.Exists(resolved))
        {
            Debug.LogWarning($"[TextureUtils] File not found: {resolved}");
            return null;
        }

        try
        {
            byte[] data = File.ReadAllBytes(resolved);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
            if (!tex.LoadImage(data))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 8;
            tex.Apply(true, false);
            return tex;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TextureUtils] LoadTexture error: {ex.Message}");
            return null;
        }
    }

    public static void ApplyBaseAndNormal(Material mat, Texture2D baseMap, Texture2D normalMap, Vector2 tiling)
    {
        if (mat == null) return;

        // Try URP Lit first
        if (mat.HasProperty("_BaseMap"))
        {
            if (baseMap) mat.SetTexture("_BaseMap", baseMap);
            mat.SetTextureScale("_BaseMap", tiling);
        }
        // Also set legacy _MainTex so both pipelines are covered
        if (mat.HasProperty("_MainTex"))
        {
            if (baseMap) mat.SetTexture("_MainTex", baseMap);
            mat.SetTextureScale("_MainTex", tiling);
        }

        if (normalMap)
        {
            if (mat.HasProperty("_BumpMap"))
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normalMap);
                mat.SetTextureScale("_BumpMap", tiling);
            }
        }
    }

    // Find first file in a folder that matches any of the provided substrings (case-insensitive)
    public static string FindTextureByHints(string folder, params string[] nameHints)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;

        // Busca solo en el nivel actual (no subcarpetas)
        var files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);

        foreach (var hint in nameHints)
        {
            var f = files.FirstOrDefault(p =>
                p.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                (p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                 p.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                 p.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrEmpty(f)) return f;
        }
        return null;
    }

}