using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CategoryItemsLoader : MonoBehaviour
{
    [Header("UI refs")]
    public RectTransform content;
    public GameObject itemButtonPrefab;

    [Header("Resources base path")]
    public string basePath = "Catalog";

    public Action OnItemsShown;
    public Action OnCategoriesShown;

    public GameObject categoriesUIPrefab;

    [Header("Back nav")]
    public Button backButton;
    public GameObject backButtonPrefab;
    public bool showBackOnItems = true;

    [Header("Sizing from Categories")]
    public RectTransform categoriesContentForSizing;

    [Header("Item sizing (se auto-sincroniza)")]
    [SerializeField] float itemPreferredWidth  = 180f;
    [SerializeField] float itemPreferredHeight = 72f;
    [SerializeField] float itemSpacing         = 8f;

    static readonly Dictionary<string, string> CatMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // sillas
        { "silla", "chair" }, { "chairs", "chair" }, { "chair", "chair" },
        // sofás
        { "sofa", "sofas" }, { "sofá", "sofas" }, { "sofas", "sofas" },
        // mesas
        { "mesa", "mesas" }, { "table", "mesas" }, { "tables", "mesas" },
        // monitores/pantallas
        { "monitor", "screens" }, { "screen", "screens" }, { "television", "screens" },
        // mouse
        { "computer_mouse", "mouse" }, { "mouse", "mouse" },
        // teclados
        { "computer_keyboard", "keyboards" }, { "keyboard", "keyboards" }, { "teclado", "keyboards" },
        // computadoras
        { "pc", "pc" }, { "computer", "pc" }, { "computadora", "pc" }, { "laptop", "pc" },
        // lámparas
        { "lampara", "lamparas" }, { "lámpara", "lamparas" }, { "lamparas", "lamparas" }, { "lamp", "lamparas" },
        // genérico niantic
        { "furniture", "furniture" },
    };

    void Awake()
    {
        ConfigureContentLayout();

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.gameObject.SetActive(false);
            backButton.onClick.AddListener(ShowCategories);
        }
    }

    void SyncSizingFromCategories()
    {
        if (categoriesContentForSizing == null) return;

        var gCat = categoriesContentForSizing.GetComponent<GridLayoutGroup>();
        if (gCat != null)
        {
            itemPreferredWidth  = gCat.cellSize.x;
            itemPreferredHeight = gCat.cellSize.y;
            itemSpacing         = Mathf.Max(gCat.spacing.x, gCat.spacing.y);
            ApplyContentLayoutParamsFromCategories();
            return;
        }

        var hCat = categoriesContentForSizing.GetComponent<HorizontalLayoutGroup>();
        if (hCat != null)
        {
            itemSpacing = hCat.spacing;

            RectTransform refChild = null;
            if (categoriesContentForSizing.childCount > 0)
                refChild = categoriesContentForSizing.GetChild(0) as RectTransform;

            if (refChild != null)
            {
                var le = refChild.GetComponent<LayoutElement>();
                if (le != null)
                {
                    if (le.preferredWidth  > 0f) itemPreferredWidth  = le.preferredWidth;
                    if (le.preferredHeight > 0f) itemPreferredHeight = le.preferredHeight;
                }
                else
                {
                    if (refChild.rect.width  > 0f) itemPreferredWidth  = refChild.rect.width;
                    if (refChild.rect.height > 0f) itemPreferredHeight = refChild.rect.height;
                }
            }

            ApplyContentLayoutParamsFromCategories(hCat.padding);
            return;
        }

        if (categoriesContentForSizing.childCount > 0)
        {
            var refChild = categoriesContentForSizing.GetChild(0) as RectTransform;
            if (refChild != null)
            {
                var le = refChild.GetComponent<LayoutElement>();
                if (le != null)
                {
                    if (le.preferredWidth  > 0f) itemPreferredWidth  = le.preferredWidth;
                    if (le.preferredHeight > 0f) itemPreferredHeight = le.preferredHeight;
                }
                else
                {
                    if (refChild.rect.width  > 0f) itemPreferredWidth  = refChild.rect.width;
                    if (refChild.rect.height > 0f) itemPreferredHeight = refChild.rect.height;
                }
            }
            ApplyContentLayoutParamsFromCategories();
        }
    }

    void ApplyContentLayoutParamsFromCategories(RectOffset paddingFromCategories = null)
    {
        if (content == null) return;

        var h = content.GetComponent<HorizontalLayoutGroup>();
        if (h != null)
        {
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.spacing = itemSpacing;

            if (paddingFromCategories != null)
                h.padding = new RectOffset(paddingFromCategories.left, paddingFromCategories.right,
                                           paddingFromCategories.top, paddingFromCategories.bottom);
            else
                h.padding = new RectOffset(4, 4, 4, 4);
        }

        var g = content.GetComponent<GridLayoutGroup>();
        if (g != null)
        {
            g.cellSize = new Vector2(itemPreferredWidth, itemPreferredHeight);
            g.spacing = new Vector2(itemSpacing, itemSpacing);
        }
    }

    void EnsureBackButtonAtStart()
    {
        if (backButton == null && backButtonPrefab != null)
        {
            var go = Instantiate(backButtonPrefab, content != null ? content : transform);
            backButton = go.GetComponent<Button>();
            if (backButton == null)
            {
                Debug.LogError("[Catalog] El prefab del BackButton no tiene componente Button en la raíz.");
                return;
            }
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(ShowCategories);
        }

        if (backButton != null && content != null && backButton.transform.parent != content)
            backButton.transform.SetParent(content, worldPositionStays: false);

        if (backButton != null)
        {
            backButton.transform.SetAsFirstSibling();
            backButton.gameObject.SetActive(showBackOnItems);
            ApplySizing(backButton.gameObject);
        }
    }

    public void ShowCategory(string rawCategory)
    {
        if (content == null || itemButtonPrefab == null)
        {
            Debug.LogError("[Catalog] Falta asignar 'content' o 'itemButtonPrefab' en el inspector.");
            return;
        }

        string cat  = NormalizeCategory(rawCategory);
        string path = $"{basePath}/{cat}".Replace("\\", "/").Trim('/');

        GameObject[] prefabs = Resources.LoadAll<GameObject>(path);
        Debug.Log($"[Catalog] Cargando {prefabs.Length} prefabs desde Resources/{path}");

        ClearContent();
        SyncSizingFromCategories();
        ConfigureContentLayout();
        EnsureBackButtonAtStart();

        foreach (var pf in prefabs)
        {
            var go = Instantiate(itemButtonPrefab, content);
            go.name = $"Item_{pf.name}";
            ApplySizing(go);

            var txt = go.GetComponentInChildren<TMP_Text>(true);
            if (txt) txt.text = PrettyName(pf.name);

            var drag = go.GetComponent<ItemDragToWorld>();
            if (!drag) drag = go.AddComponent<ItemDragToWorld>();
            drag.SetItemPrefab(pf);

            var btn = go.GetComponentInChildren<Button>(true);
            if (btn)
            {
                btn.onClick.RemoveAllListeners();
                var localPrefab = pf;
                btn.onClick.AddListener(() =>
                {
                    Debug.Log($"[Catalog] Seleccionado: {localPrefab.name}");
                    if (ObjectPlacer.Instance != null)
                        ObjectPlacer.Instance.BeginPlacement(localPrefab);
                    else
                        Debug.LogError("[Catalog] ObjectPlacer no encontrado en la escena");
                });
            }
        }

        OnItemsShown?.Invoke();
    }

    public void ShowCategories()
    {
        ClearContent();
        if (backButton != null) backButton.gameObject.SetActive(false);

        var cats = Instantiate(categoriesUIPrefab, content);
        cats.name = "DetectedCategoriesUI";

        OnCategoriesShown?.Invoke();
    }

    string NormalizeCategory(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "unknown";

        string s = RemoveDiacritics(raw).Trim().ToLowerInvariant();
        s = s.Replace("_", " ").Replace("-", " ").Replace("/", " ").Trim();

        string[] parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string key = parts.Length > 0 ? parts[0] : s;

        if (CatMap.TryGetValue(key, out string mapped))
            return mapped;

        return key;
    }

    string PrettyName(string raw)
    {
        string s = raw.Replace("_", " ");
        if (s.Length > 0)
            s = char.ToUpper(s[0]) + s.Substring(1);
        return s;
    }

    static string RemoveDiacritics(string text)
    {
        var norm = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(capacity: norm.Length);
        for (int i = 0; i < norm.Length; i++)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(norm[i]);
            if (uc != UnicodeCategory.NonSpacingMark)
                sb.Append(norm[i]);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private void ClearContent()
    {
        if (content == null) return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i);
            if (backButton != null && child == backButton.transform)
                continue;
            Destroy(child.gameObject);
        }
    }

    void ConfigureContentLayout()
    {
        if (content == null) return;

        var h = content.GetComponent<HorizontalLayoutGroup>();
        if (h != null)
        {
            h.childControlWidth     = true;
            h.childControlHeight    = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight= false;
            h.spacing               = itemSpacing;
            if (h.padding == null) h.padding = new RectOffset(4,4,4,4);
        }

        var g = content.GetComponent<GridLayoutGroup>();
        if (g != null)
        {
            g.cellSize = new Vector2(itemPreferredWidth, itemPreferredHeight);
            g.spacing  = new Vector2(itemSpacing, itemSpacing);
        }
    }

    void ApplySizing(GameObject go)
    {
        if (go == null) return;

        var rt = go.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(rt.anchorMin.x, 0.5f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 0.5f);
            rt.pivot    = new Vector2(0.5f, 0.5f);
        }

        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();

        le.minWidth        = itemPreferredWidth;
        le.minHeight       = itemPreferredHeight;
        le.preferredWidth  = itemPreferredWidth;
        le.preferredHeight = itemPreferredHeight;
        le.flexibleWidth   = 0;
        le.flexibleHeight  = 0;
    }
}
