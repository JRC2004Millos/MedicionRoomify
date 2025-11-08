using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[Serializable] public class DetectionPosition { public float x; public float y; }
[Serializable] public class DetectionEntry { public string categoryName; public float confidence; public string timestamp; public DetectionPosition position; }
[Serializable] public class DetectionWrapper {
    public List<DetectionEntry> detections = new List<DetectionEntry>();
    public int sourceScreenWidth = 0;
    public int sourceScreenHeight = 0;
    public bool coordsAreNormalized = true;
    public bool yOriginIsTop = true;
}

public class DetectedCategoriesFromJson : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RectTransform categoriesContent;      // ← arrastra CategoriesContent (el del scroll)
    [SerializeField] private GameObject categoryButtonPrefab;      // ← tu CategoryButtonPrefab (con Label/Icon)
    [SerializeField] private bool clearContentBeforeBuild = true;

    [Header("Destino de ítems")]
    [SerializeField] private CategoryItemsLoader itemsLoader;      // ← arrastra el CatalogRunTime (CategoryItemsLoader)

    [Header("Archivo JSON de detecciones")]
    [Tooltip("Si lo dejas vacío, usa StreamingAssets/detected_objects.json o persistentDataPath")]
    [SerializeField] private string absoluteJsonPathOverride = "";
    [SerializeField] private string detectionsJsonFileName = "detected_objects.json";

    [Header("Iconos opcionales (Resources/CategoryIcons/<Name>.png)")]
    [SerializeField] private bool useIcons = true;

    [Header("Auto")]
    [SerializeField] private bool buildOnStart = true;

    DetectionWrapper _cache;

    void Start()
    {
        if (buildOnStart) BuildFromJson();
    }

    public void BuildFromJson()
    {
        // 1) Cargar JSON
        var wrapper = LoadDetections();
        _cache = wrapper;

        // 2) Categorías únicas (case-insensitive)
        var cats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (wrapper?.detections != null)
            foreach (var d in wrapper.detections)
                if (!string.IsNullOrWhiteSpace(d.categoryName)) cats.Add(d.categoryName.Trim());

        // Fallback si no trae nada
        if (cats.Count == 0)
        {
            Debug.LogWarning("[DetectedCategoriesFromJson] No se encontraron categorías en el JSON.");
            return;
        }

        // 3) Limpiar contenedor
        if (clearContentBeforeBuild)
            for (int i = categoriesContent.childCount - 1; i >= 0; i--)
                Destroy(categoriesContent.GetChild(i).gameObject);

        // 4) Crear un botón por categoría (ordenado alfabéticamente)
        foreach (var cat in cats.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
            SpawnCategoryButton(cat);
    }

    // ================= helpers =================

    private DetectionWrapper LoadDetections()
    {
        string path = ResolveDetectionsPath();
        if (!File.Exists(path))
        {
            Debug.LogError($"[DetectedCategoriesFromJson] No se encontró el JSON en: {path}");
            return new DetectionWrapper();
        }
        try
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<DetectionWrapper>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DetectedCategoriesFromJson] Error leyendo JSON: {ex.Message}");
            return new DetectionWrapper();
        }
    }

    private string ResolveDetectionsPath()
    {
        if (!string.IsNullOrEmpty(absoluteJsonPathOverride)) return absoluteJsonPathOverride;

#if UNITY_ANDROID && !UNITY_EDITOR
        string candidate = Path.Combine(Application.persistentDataPath, detectionsJsonFileName);
        if (File.Exists(candidate)) return candidate;
        return candidate; // en Android el StreamingAssets requiere WWW/UnityWebRequest; usamos persistent
#else
        string streaming = Path.Combine(Application.streamingAssetsPath, detectionsJsonFileName);
        if (File.Exists(streaming)) return streaming;
        string persistent = Path.Combine(Application.persistentDataPath, detectionsJsonFileName);
        if (File.Exists(persistent)) return persistent;
        // último recurso: ruta relativa dentro del proyecto (Editor)
        return streaming;
#endif
    }

    private void SpawnCategoryButton(string jsonCategoryName)
    {
        var go = Instantiate(categoryButtonPrefab, categoriesContent);
        go.name = $"Cat_{jsonCategoryName}";

        // Texto
        var txt = go.GetComponentInChildren<TMP_Text>(true);
        if (txt) txt.text = Pretty(jsonCategoryName);

        // Icono
        if (useIcons)
        {
            var icon = go.transform.Find("Icon")?.GetComponent<Image>();
            if (icon)
            {
                var iconKey = NormalizeForIcon(jsonCategoryName);
                var sprite = Resources.Load<Sprite>($"CategoryIcons/{iconKey}");
                if (sprite != null) { icon.sprite = sprite; icon.preserveAspect = true; icon.enabled = true; }
                else icon.enabled = false;
            }
        }

        // Click → cargar ítems de la carpeta que mapea esa categoría
        var btn = go.GetComponent<Button>();
        if (btn)
            btn.onClick.AddListener(() =>
            {
                string folder = NormalizeForFolder(jsonCategoryName);
                itemsLoader.ShowCategory(folder);  // ← carga Resources/Catalog/<folder>/*
            });
    }

    private static string Pretty(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var s = raw.Replace("_", " ").Trim();
        return char.ToUpper(s[0]) + s.Substring(1);
    }

    // Mapea NOMBRES DEL JSON a nombres de archivo de icono
    private static string NormalizeForIcon(string raw)
    {
        string s = raw.Trim().ToLowerInvariant();
        return s switch
        {
            "computer_mouse" => "Mouse",
            "computer_keyboard" => "Keyboard",
            "screen" or "monitor" or "television" => "Monitor",
            "chair" => "Chair",
            "sofa" or "couch" => "Sofa",
            "table" => "Table",
            "lamp" or "lampara" or "lámpara" => "Lamp",
            "plant" or "potted_plant" => "Plant",
            "furniture" => "Furniture",
            _ => char.ToUpper(s[0]) + s.Substring(1)
        };
    }

    // Mapea NOMBRES DEL JSON a carpetas bajo Resources/Catalog/<folder>
    public static string NormalizeForFolder(string raw)
    {
        string s = raw.Trim().ToLowerInvariant();
        return s switch
        {
            "computer_mouse" => "Mouse",
            "computer_keyboard" => "Keyboard",
            "screen" or "monitor" or "television" => "Monitor",
            "potted_plant" or "plant" => "Plant",
            "sofa" or "couch" => "Sofa",
            "lamp" or "lampara" or "lámpara" => "Lamp",
            "table" => "Table",
            "chair" => "chair",          // ← si tu carpeta está en minúsculas, respétalo
            "furniture" => "furniture",  // ← idem
            _ => char.ToUpper(s[0]) + s.Substring(1)
        };
    }
}
