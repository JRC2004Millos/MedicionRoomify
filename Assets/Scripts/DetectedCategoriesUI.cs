using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class StringEvent : UnityEvent<string> { }

public class DetectedCategoriesUI : MonoBehaviour
{
    [Header("UI refs")]
    [SerializeField] private RectTransform categoriesContent;
    [SerializeField] private GameObject categoryButtonPrefab;

    [Header("Detections JSON")]
    [SerializeField] private string detectionsJsonFileName = "detected_objects.json";
    [SerializeField] private string absoluteJsonPathOverride = "";

    [Header("Output")]
    public StringEvent OnCategorySelected = new StringEvent();
    public CategoryItemsLoader catalogLoader; // ⬅️ AÑADE ESTO
    private readonly HashSet<string> _cats = new HashSet<string>();

    private string ResolvePath()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!string.IsNullOrEmpty(absoluteJsonPathOverride)) return absoluteJsonPathOverride;
        return Path.Combine(Application.persistentDataPath, detectionsJsonFileName);
#else
        if (!string.IsNullOrEmpty(absoluteJsonPathOverride)) return absoluteJsonPathOverride;

        string p1 = Path.Combine(Application.streamingAssetsPath, detectionsJsonFileName);
        if (File.Exists(p1)) return p1;

        return Path.Combine(Application.dataPath, "StreamingAssets", detectionsJsonFileName);
#endif
    }

    private void OnEnable()
    {
        BuildCategoriesFromJson();
    }

    public void BuildCategoriesFromJson()
    {
        _cats.Clear();
        foreach (Transform t in categoriesContent) Destroy(t.gameObject);

        string path = ResolvePath();
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[DetectedCategoriesUI] JSON no encontrado: {path}");
            return;
        }

        string json = File.ReadAllText(path);
        var wrapper = JsonUtility.FromJson<DetectionWrapper>(json);
        if (wrapper == null || wrapper.detections == null) return;

        foreach (var d in wrapper.detections)
        {
            if (d == null || string.IsNullOrEmpty(d.categoryName)) continue;
            _cats.Add(d.categoryName.Trim());
        }

        foreach (var cat in _cats)
            SpawnCategoryButton(cat);
    }

    private void SpawnCategoryButton(string category)
    {
        var go = Instantiate(categoryButtonPrefab, categoriesContent);
        go.name = $"Cat_{category}";
        var txt = go.GetComponentInChildren<TMP_Text>(true);
        if (txt) txt.text = CategoryPrettyName(category);

        var btn = go.GetComponent<Button>();
        if (btn)
        {
            btn.onClick.AddListener(() =>
            {
                OnCategorySelected.Invoke(category);

                // ⬅️ AÑADE ESTO: llama directamente al loader
                if (catalogLoader != null)
                    catalogLoader.ShowCategory(category);
            });
        }
    }

    private string CategoryPrettyName(string raw)
    {
        // Solo para mostrar bonito en español
        string s = raw.Trim().ToLowerInvariant();
        return s switch
        {
            "chair" => "Silla",
            "sofa" => "Sofá",
            "table" => "Mesa",
            "screen" or "monitor" or "television" => "Monitor",
            "computer_mouse" or "mouse" => "Mouse",
            "computer_keyboard" or "keyboard" => "Teclado",
            "lamp" or "lampara" or "lámpara" => "Lámpara",
            "plant" => "Planta",
            _ => char.ToUpper(raw[0]) + raw.Substring(1)
        };
    }

    // === Clases JSON (mismas que usas ya) ===
    [System.Serializable] public class DetectionWrapper { public List<DetectionEntry> detections; }
    [System.Serializable] public class DetectionEntry { public string categoryName; public float confidence; public string timestamp; public DetectionPosition position; }
    [System.Serializable] public class DetectionPosition { public float x; public float y; }
}
