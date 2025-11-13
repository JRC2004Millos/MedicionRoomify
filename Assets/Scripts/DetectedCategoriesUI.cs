using System;
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
    public CategoryItemsLoader catalogLoader;
    private readonly HashSet<string> _cats = new HashSet<string>();

    [SerializeField] private Button toggleBarButton;
    [SerializeField] private RectTransform barContainer;
    private bool _barHidden = false;

    [Header("Full catalog mode")]
    [Tooltip("Listado de TODAS las categorías disponibles (nombres como los que vienen del JSON).")]
    [SerializeField] private List<string> allCatalogCategories = new List<string>
    {
        "chair",
        "sofa",
        "table",
        "screen",
        "computer_mouse",
        "computer_keyboard",
        "lamp",
        "plant",
        "pc",
        "furniture"
    };

    [SerializeField] private Button ConfirmButton;

    private bool _showingDetectedOnly = true;

    private void EnsureConfirmButtonAtStart()
    {
        if (ConfirmButton != null)
        {
            ConfirmButton.transform.SetAsLastSibling();
            return;
        }

        if (categoriesContent == null || categoryButtonPrefab == null)
        {
            Debug.LogWarning("[DetectedCategoriesUI] No puedo crear ConfirmButton (falta categoriesContent o categoryButtonPrefab).");
            return;
        }

        var go = Instantiate(categoryButtonPrefab, categoriesContent);
        go.name = "ConfirmButton";

        var txt = go.GetComponentInChildren<TMP_Text>(true);
        if (txt != null) txt.text = "Confirmar";

        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();

        ConfirmButton = btn;
        ConfirmButton.transform.SetAsLastSibling();

        Debug.Log("[DetectedCategoriesUI] ConfirmButton creado automáticamente");
    }

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
        _showingDetectedOnly = true;

        EnsureConfirmButtonAtStart();

        if (ConfirmButton != null)
        {
            ConfirmButton.gameObject.SetActive(true);
            ConfirmButton.onClick.RemoveAllListeners();
            ConfirmButton.onClick.AddListener(OnConfirmClicked);
        }

        if (toggleBarButton != null)
        {
            toggleBarButton.onClick.RemoveAllListeners();
            toggleBarButton.onClick.AddListener(ToggleBarVisibility);
            toggleBarButton.gameObject.SetActive(false);
        }

        BuildCategoriesFromJson();
    }

    public void BuildCategoriesFromJson()
    {
        _cats.Clear();

        foreach (Transform t in categoriesContent)
        {
            if (ConfirmButton != null)
            {
                if (t == ConfirmButton.transform || ConfirmButton.transform.IsChildOf(t))
                    continue;
            }

            Destroy(t.gameObject);
        }

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

        if (ConfirmButton != null)
            ConfirmButton.transform.SetAsLastSibling();
    }

    private void Awake()
    {
        // --- Reasignar SIEMPRE el ConfirmButton en el clon ---
        ConfirmButton = null;

        foreach (var b in GetComponentsInChildren<Button>(true))
        {
            if (b.name.IndexOf("confirm", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ConfirmButton = b;
                Debug.Log($"[DetectedCategoriesUI] Auto-asignado ConfirmButton (instancia): {ConfirmButton.name}");
                break;
            }
        }

        if (ConfirmButton == null)
            Debug.LogWarning("[DetectedCategoriesUI] No se encontró ningún botón de confirmar (hijo con 'confirm' en el nombre).");

        if (barContainer == null)
        {
            if (categoriesContent != null)
            {
                barContainer = categoriesContent.parent as RectTransform;
                if (barContainer != null)
                    Debug.Log($"[DetectedCategoriesUI] Auto-asignado barContainer = {barContainer.name}");
            }

            if (barContainer == null)
            {
                barContainer = GetComponent<RectTransform>();
                Debug.LogWarning("[DetectedCategoriesUI] barContainer no estaba asignado; uso el RectTransform propio.");
            }
        }
    }

    private void OnConfirmClicked()
    {
        if (!_showingDetectedOnly) return;

        _showingDetectedOnly = false;
        BuildAllCategoriesBar();

        if (ConfirmButton != null)
            ConfirmButton.gameObject.SetActive(false);

        if (toggleBarButton != null)
        {
            _barHidden = false;

            if (barContainer != null)
                barContainer.gameObject.SetActive(true);

            toggleBarButton.gameObject.SetActive(true);
        }
    }

    private void BuildAllCategoriesBar()
    {
        _cats.Clear();

        foreach (Transform t in categoriesContent)
        {
            if (ConfirmButton != null)
            {
                if (t == ConfirmButton.transform || ConfirmButton.transform.IsChildOf(t))
                    continue;
            }

            Destroy(t.gameObject);
        }

        foreach (var cat in allCatalogCategories)
        {
            if (string.IsNullOrWhiteSpace(cat)) continue;
            _cats.Add(cat.Trim());
        }

        foreach (var cat in _cats)
            SpawnCategoryButton(cat);

        if (ConfirmButton != null)
            ConfirmButton.transform.SetAsLastSibling();
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

    private void ToggleBarVisibility()
    {
        if (_showingDetectedOnly)
            return;

        _barHidden = !_barHidden;

        if (barContainer != null)
        {
            barContainer.gameObject.SetActive(!_barHidden);
            Debug.Log($"[DetectedCategoriesUI] ToggleBarVisibility -> barHidden={_barHidden}, target={barContainer.name}");
        }

        if (toggleBarButton != null)
        {
            var txt = toggleBarButton.GetComponentInChildren<TMP_Text>();
            if (txt != null)
                txt.text = _barHidden ? "Mostrar" : "Ocultar";
        }
    }

    [System.Serializable] public class DetectionWrapper { public List<DetectionEntry> detections; }
    [System.Serializable] public class DetectionEntry { public string categoryName; public float confidence; public string timestamp; public DetectionPosition position; }
    [System.Serializable] public class DetectionPosition { public float x; public float y; }
}
