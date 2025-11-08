using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CatalogUI : MonoBehaviour
{
    [Header("Data")]
    public CatalogDatabase database;

    [Header("Scene Placement")]
    public RoomDropPlacer dropPlacer;

    [Header("UI References")]
    public RectTransform categoriesContent; // content del ScrollRect de categorías
    public RectTransform itemsContent;      // content del ScrollRect de items
    public GameObject categoryButtonPrefab; // UI_CategoryButton
    public GameObject itemCardPrefab;       // UI_ItemCard
    public Canvas dragCanvas;               // canvas para el ghost
    public Image dragGhostImage;            // imagen que sigue el puntero

    private int currentCategoryIndex = -1;
    private readonly List<GameObject> _spawnedCategoryButtons = new();
    private readonly List<GameObject> _spawnedItemCards = new();

    void Start()
    {
        if (dropPlacer == null) dropPlacer = FindFirstObjectByType<RoomDropPlacer>();
        BuildCategories();
        if (database != null && database.categories.Count > 0) ShowCategory(0);

        if (dragGhostImage != null) dragGhostImage.enabled = false;
    }

    void BuildCategories()
    {
        ClearList(_spawnedCategoryButtons);
        if (database == null) return;

        for (int i = 0; i < database.categories.Count; i++)
        {
            var cat = database.categories[i];
            var go = Instantiate(categoryButtonPrefab, categoriesContent);
            _spawnedCategoryButtons.Add(go);

            // Vincula UI
            var btn = go.GetComponent<Button>();
            var img = go.transform.Find("Icon")?.GetComponent<Image>();
            var txt = go.transform.Find("Label")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (img) img.sprite = cat.categoryIcon;
            if (txt) txt.text = cat.categoryName;
            int idx = i;
            btn.onClick.AddListener(() => ShowCategory(idx));
        }
    }

    void ShowCategory(int idx)
    {
        if (database == null || idx < 0 || idx >= database.categories.Count) return;
        currentCategoryIndex = idx;

        ClearList(_spawnedItemCards);

        var cat = database.categories[idx];
        foreach (var item in cat.items)
        {
            var go = Instantiate(itemCardPrefab, itemsContent);
            _spawnedItemCards.Add(go);

            var img = go.transform.Find("Thumb")?.GetComponent<Image>();
            var txt = go.transform.Find("Label")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (img) img.sprite = item.thumbnail;
            if (txt) txt.text = item.displayName;

            // Drag handler
            var drag = go.AddComponent<UIItemDragHandler>();
            drag.Init(this, item);
        }
    }

    void ClearList(List<GameObject> list)
    {
        foreach (var go in list) if (go) Destroy(go);
        list.Clear();
    }

    // === API usada por UIItemDragHandler ===
    public void BeginDragItem(CatalogItem item, Vector2 screenPos)
    {
        if (dragGhostImage)
        {
            dragGhostImage.enabled = true;
            dragGhostImage.sprite = item.thumbnail;
            dragGhostImage.SetNativeSize();
            dragGhostImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            dragGhostImage.rectTransform.position = screenPos;
        }
    }
    public void UpdateDragItem(Vector2 screenPos)
    {
        if (dragGhostImage) dragGhostImage.rectTransform.position = screenPos;
    }
    public void EndDragItem(CatalogItem item, Vector2 screenPos)
    {
        if (dragGhostImage) dragGhostImage.enabled = false;

        if (dropPlacer != null && item != null && item.prefab != null)
        {
            if (dropPlacer.TryPlaceAtPointer(item.prefab, screenPos, out var spawned))
            {
                // (Opcional) seleccionar, aplicar escala inicial, etc.
            }
        }
    }
}

/// Componente que se acopla a cada tarjeta de item y maneja el drag
public class UIItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private CatalogUI catalogUI;
    private CatalogItem item;

    public void Init(CatalogUI ui, CatalogItem i)
    {
        catalogUI = ui; item = i;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        catalogUI.BeginDragItem(item, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        catalogUI.UpdateDragItem(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        catalogUI.EndDragItem(item, eventData.position);
    }
}
