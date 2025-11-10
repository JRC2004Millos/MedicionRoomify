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

    public System.Action OnItemsShown;   // para avisar que estamos en vista de ítems
    public System.Action OnCategoriesShown; // para avisar que volvimos a categorías

    public GameObject categoriesUIPrefab;

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

    public void ShowCategory(string rawCategory)
    {
        if (content == null || itemButtonPrefab == null)
        {
            Debug.LogError("[Catalog] Falta asignar 'content' o 'itemButtonPrefab' en el inspector.");
            return;
        }

        string cat = NormalizeCategory(rawCategory);
        string path = $"{basePath}/{cat}".Replace("\\", "/").Trim('/');

        GameObject[] prefabs = Resources.LoadAll<GameObject>(path);
        Debug.Log($"[Catalog] Cargando {prefabs.Length} prefabs desde Resources/{path}");

        if (prefabs.Length == 0)
        {
            Debug.LogWarning($"[Catalog] No se encontraron prefabs en Resources/{path}");
        }

        // Limpiar UI anterior
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        // Crear botones
        foreach (var pf in prefabs)
        {
            var go = Instantiate(itemButtonPrefab, content);
            go.name = $"Item_{pf.name}";

            // Texto
            var txt = go.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (txt) txt.text = PrettyName(pf.name);

            // Drag & Drop hacia el mundo
            var drag = go.GetComponent<ItemDragToWorld>();
            if (!drag) drag = go.AddComponent<ItemDragToWorld>();
            drag.SetItemPrefab(pf); // <- este es el prefab real a soltar

            // Botón con click (si quieres mantener el flujo por tap)
            var btn = go.GetComponentInChildren<Button>(true);
            if (btn)
            {
                btn.onClick.RemoveAllListeners();
                var localPrefab = pf; // capturar referencia

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
        // limpia contenido actual
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        // instancia el panel de categorías original
        var cats = Instantiate(categoriesUIPrefab, content);
        cats.name = "DetectedCategoriesUI";

        OnCategoriesShown?.Invoke(); // <- avisa que oculte el botón Volver
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
        // Eliminar números y guiones bajos
        string s = raw.Replace("_", " ");

        // Capitalizar primera letra
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
}