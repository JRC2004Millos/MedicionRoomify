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

    [Header("Guardar espacio")]
    [SerializeField] private GameObject SaveButton;
    private Button _saveRuntimeButton;
    [SerializeField] private GameObject saveDialogPanel;
    [SerializeField] private TMP_InputField saveNameInput;
    [SerializeField] private Button saveDialogConfirmButton;
    [SerializeField] private Button saveDialogCancelButton;

    [SerializeField] private RoomSaving roomSaving;

    [Header("Full catalog mode")]
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
    public static bool HasConfirmedAtLeastOnce = false;

    private enum BarMode
    {
        DetectedOnly,
        FullCatalog
    }

    private BarMode _currentMode = BarMode.DetectedOnly;

    private void Awake()
    {
        ConfirmButton = null;

        if (categoriesContent != null)
        {
            foreach (var b in categoriesContent.GetComponentsInChildren<Button>(true))
            {
                if (b.name.IndexOf("confirm", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ConfirmButton = b;
                    break;
                }
            }
        }

        if (barContainer == null)
        {
            if (categoriesContent != null)
            {
                barContainer = categoriesContent.parent as RectTransform;
            }

            if (barContainer == null)
            {
                barContainer = GetComponent<RectTransform>();
            }
        }
    }

    private void EnsureConfirmButtonAtStart()
    {
        if (ConfirmButton != null)
        {
            ConfirmButton.transform.SetAsLastSibling();
            return;
        }

        if (categoriesContent == null || categoryButtonPrefab == null)
        {
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
        EnsureConfirmButtonAtStart();

        if (ConfirmButton != null)
        {
            ConfirmButton.onClick.RemoveAllListeners();
            ConfirmButton.onClick.AddListener(OnConfirmClicked);
        }

        if (toggleBarButton != null)
        {
            toggleBarButton.onClick.RemoveAllListeners();
            toggleBarButton.onClick.AddListener(ToggleBarVisibility);
        }

        if (saveDialogPanel != null)
            saveDialogPanel.SetActive(false);

        if (saveDialogConfirmButton != null)
        {
            saveDialogConfirmButton.onClick.RemoveAllListeners();
            saveDialogConfirmButton.onClick.AddListener(OnSaveDialogConfirm);
        }

        if (saveDialogCancelButton != null)
        {
            saveDialogCancelButton.onClick.RemoveAllListeners();
            saveDialogCancelButton.onClick.AddListener(OnSaveDialogCancel);
        }

        _currentMode = HasConfirmedAtLeastOnce ? BarMode.FullCatalog : BarMode.DetectedOnly;

        RefreshBar();
    }

    private void Update()
    {
        if (_currentMode == BarMode.DetectedOnly && ConfirmButton != null)
        {
            if (!ConfirmButton.gameObject.activeSelf)
                ConfirmButton.gameObject.SetActive(true);

            if (!ConfirmButton.interactable)
                ConfirmButton.interactable = true;

            ConfirmButton.transform.SetAsLastSibling();
        }

        if (_currentMode == BarMode.FullCatalog)
        {
            if (_saveRuntimeButton == null || _saveRuntimeButton.gameObject == null)
            {
                EnsureSaveButtonForFullCatalog();
            }
            else
            {
                if (!_saveRuntimeButton.gameObject.activeSelf)
                    _saveRuntimeButton.gameObject.SetActive(true);
            }
        }
    }

    public void RefreshBar()
    {
        if (_currentMode == BarMode.DetectedOnly)
        {
            if (ConfirmButton == null || ConfirmButton.gameObject == null)
            {
                EnsureConfirmButtonAtStart();
            }

            if (ConfirmButton != null)
            {
                ConfirmButton.gameObject.SetActive(true);
                ConfirmButton.interactable = true;

                ConfirmButton.onClick.RemoveAllListeners();
                ConfirmButton.onClick.AddListener(OnConfirmClicked);

                ConfirmButton.transform.SetAsLastSibling();
            }

            if (toggleBarButton != null)
                toggleBarButton.gameObject.SetActive(false);

            BuildCategoriesFromJson();

            if (_saveRuntimeButton != null && _saveRuntimeButton.gameObject != null)
                _saveRuntimeButton.gameObject.SetActive(false);

            if (SaveButton != null)
                SaveButton.SetActive(false);
        }
        else
        {
            if (ConfirmButton != null)
                ConfirmButton.gameObject.SetActive(false);

            if (toggleBarButton != null)
            {
                _barHidden = false;
                toggleBarButton.gameObject.SetActive(true);
            }

            BuildAllCategoriesBar();
            EnsureSaveButtonForFullCatalog();

            if (barContainer != null)
                barContainer.gameObject.SetActive(true);
        }
    }

    public void BuildCategoriesFromJson()
    {
        _cats.Clear();

        foreach (Transform t in categoriesContent)
        {
            if (ConfirmButton != null &&
                (t == ConfirmButton.transform || ConfirmButton.transform.IsChildOf(t)))
                continue;

            Destroy(t.gameObject);
        }

        string path = ResolvePath();
        if (!File.Exists(path))
        {
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

        if (SaveButton != null)
            SaveButton.SetActive(false);
    }

    private void BuildAllCategoriesBar()
    {
        _cats.Clear();

        foreach (Transform t in categoriesContent)
        {
            if (ConfirmButton != null &&
                (t == ConfirmButton.transform || ConfirmButton.transform.IsChildOf(t)))
                continue;

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

                if (catalogLoader != null)
                    catalogLoader.ShowCategory(category);
            });
        }
    }

    public void OnConfirmClicked()
    {
        HasConfirmedAtLeastOnce = true;
        _currentMode = BarMode.FullCatalog;
        RefreshBar();
    }

    public void ForceFullCatalogMode()
    {
        HasConfirmedAtLeastOnce = true;
        _currentMode = BarMode.FullCatalog;
        RefreshBar();
    }

    private void ToggleBarVisibility()
    {
        if (_currentMode == BarMode.DetectedOnly)
            return;

        _barHidden = !_barHidden;

        if (barContainer != null)
        {
            barContainer.gameObject.SetActive(!_barHidden);
        }

        if (toggleBarButton != null)
        {
            var txt = toggleBarButton.GetComponentInChildren<TMP_Text>();
            if (txt != null)
                txt.text = _barHidden ? "Mostrar" : "Ocultar";
        }
    }

    private void OnSaveSpaceClicked()
    {
        Debug.Log("[DetectedCategoriesUI] OnSaveSpaceClicked()");

        if (_currentMode != BarMode.FullCatalog)
        {
            Debug.LogWarning("[DetectedCategoriesUI] Ignorando guardado: no estamos en FullCatalog.");
            return;
        }

        if (saveDialogPanel != null)
        {
            saveDialogPanel.SetActive(true);
            Debug.Log("[DetectedCategoriesUI] Mostrando panel de guardado.");
        }
        else
        {
            Debug.LogError("[DetectedCategoriesUI] saveDialogPanel es NULL, asígnalo en el inspector.");
        }

        if (saveNameInput != null)
            saveNameInput.text = "";
        else
            Debug.LogError("[DetectedCategoriesUI] saveNameInput es NULL, asígnalo en el inspector.");
    }

    private void OnSaveDialogConfirm()
    {
        Debug.Log("[DetectedCategoriesUI] OnSaveDialogConfirm() llamado.");

        if (saveNameInput == null)
        {
            Debug.LogError("[DetectedCategoriesUI] saveNameInput es NULL, revisa la referencia en el inspector.");
            return;
        }

        string nombre = saveNameInput.text.Trim();
        Debug.Log($"[DetectedCategoriesUI] Texto recibido en el input: '{nombre}'");

        if (string.IsNullOrEmpty(nombre))
        {
            Debug.LogWarning("[DetectedCategoriesUI] Nombre vacío, no se guarda.");
            return;
        }

        if (roomSaving == null)
        {
            Debug.LogError("[DetectedCategoriesUI] roomSaving es NULL, asigna el componente RoomSaving en el inspector.");
        }
        else
        {
            Debug.Log("[DetectedCategoriesUI] roomSaving encontrado, procediendo a guardar...");

            roomSaving.spaceName   = nombre;
            roomSaving.roomId      = nombre;
            roomSaving.saveFileName = nombre + ".json";

            try
            {
                roomSaving.SaveRoom();
            }
            catch (Exception ex)
            {
                Debug.LogError("[DetectedCategoriesUI] Excepción al llamar a SaveRoom(): " + ex);
            }
        }

    #if UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log("[DetectedCategoriesUI] En Android: llamando a Application.Quit().");
        Application.Quit();
    #endif

    #if UNITY_EDITOR
        Debug.Log("[DetectedCategoriesUI] En Editor: deteniendo modo Play.");
        UnityEditor.EditorApplication.isPlaying = false;
    #endif

        if (saveDialogPanel != null)
        {
            saveDialogPanel.SetActive(false);
            Debug.Log("[DetectedCategoriesUI] Ocultando panel de guardado.");
        }
        else
        {
            Debug.LogWarning("[DetectedCategoriesUI] saveDialogPanel es NULL al intentar ocultarlo.");
        }
    }

    private void OnSaveDialogCancel()
    {
        if (saveDialogPanel != null)
            saveDialogPanel.SetActive(false);
    }

    private void EnsureSaveButtonForFullCatalog()
    {
        if (categoriesContent == null)
        {
            return;
        }

        GameObject prefab = null;

        if (SaveButton != null)
            prefab = SaveButton;
        else if (categoryButtonPrefab != null)
            prefab = categoryButtonPrefab;

        if (prefab == null)
        {
            return;
        }

        if (_saveRuntimeButton == null || _saveRuntimeButton.gameObject == null)
        {
            var go = Instantiate(prefab, categoriesContent);
            go.name = "SaveSpaceButton";

            var txt = go.GetComponentInChildren<TMP_Text>(true);
            if (txt != null)
                txt.text = "Guardar";

            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();

            _saveRuntimeButton = btn;
            _saveRuntimeButton.onClick.RemoveAllListeners();
            _saveRuntimeButton.onClick.AddListener(OnSaveSpaceClicked);
        }
        else
        {
            _saveRuntimeButton.onClick.RemoveAllListeners();
            _saveRuntimeButton.onClick.AddListener(OnSaveSpaceClicked);
        }

        _saveRuntimeButton.gameObject.SetActive(true);
        _saveRuntimeButton.transform.SetAsLastSibling();
    }

    private string CategoryPrettyName(string raw)
    {
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

    [System.Serializable] public class DetectionWrapper { public List<DetectionEntry> detections; }
    [System.Serializable] public class DetectionEntry { public string categoryName; public float confidence; public string timestamp; public DetectionPosition position; }
    [System.Serializable] public class DetectionPosition { public float x; public float y; }
}
