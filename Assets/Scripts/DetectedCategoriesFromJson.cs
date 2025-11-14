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
    [SerializeField] private RectTransform categoriesContent;
    [SerializeField] private GameObject categoryButtonPrefab;
    [SerializeField] private bool clearContentBeforeBuild = true;

    [Header("Destino de ítems")]
    [SerializeField] private CategoryItemsLoader itemsLoader;

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
        var wrapper = LoadDetections();
        _cache = wrapper;

        var cats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (wrapper?.detections != null)
            foreach (var d in wrapper.detections)
                if (!string.IsNullOrWhiteSpace(d.categoryName)) cats.Add(d.categoryName.Trim());

        if (cats.Count == 0)
        {
            Debug.LogWarning("[DetectedCategoriesFromJson] No se encontraron categorías en el JSON.");
            return;
        }

        if (clearContentBeforeBuild)
            for (int i = categoriesContent.childCount - 1; i >= 0; i--)
                Destroy(categoriesContent.GetChild(i).gameObject);

        foreach (var cat in cats.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
            SpawnCategoryButton(cat);
    }

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
        return candidate;
#else
        string streaming = Path.Combine(Application.streamingAssetsPath, detectionsJsonFileName);
        if (File.Exists(streaming)) return streaming;
        string persistent = Path.Combine(Application.persistentDataPath, detectionsJsonFileName);
        if (File.Exists(persistent)) return persistent;
        return streaming;
#endif
    }

    private void SpawnCategoryButton(string jsonCategoryName)
    {
        var go = Instantiate(categoryButtonPrefab, categoriesContent);
        go.name = $"Cat_{jsonCategoryName}";

        var txt = go.GetComponentInChildren<TMP_Text>(true);
        if (txt) txt.text = Pretty(jsonCategoryName);

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

        var btn = go.GetComponent<Button>();
        if (btn)
            btn.onClick.AddListener(() =>
            {
                string folder = NormalizeForFolder(jsonCategoryName);
                itemsLoader.ShowCategory(folder);
            });
    }

    private static string Pretty(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        var s = raw.Replace("_", " ").Trim();
        return char.ToUpper(s[0]) + s.Substring(1);
    }

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
            "chair" => "chair",
            "furniture" => "furniture",
            _ => char.ToUpper(s[0]) + s.Substring(1)
        };
    }
}
