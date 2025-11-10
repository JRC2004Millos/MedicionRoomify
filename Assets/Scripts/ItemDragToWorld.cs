using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemDragToWorld : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("World drop")]
    [SerializeField] private RoomDropPlacer dropPlacer;

    [Header("Ghost UI")]
    [SerializeField] private Image dragGhostImage;

    [Header("Item")]
    [SerializeField] private GameObject itemPrefab;   // El prefab a soltar
    [SerializeField] private Image itemThumb;         // La imagen del botón (para el ghost)

    Camera cam;

    void Awake()
    {
        // Autowire para minimizar cableado en el inspector
        if (!dropPlacer) dropPlacer = FindObjectOfType<RoomDropPlacer>();
        if (!dragGhostImage)
        {
            var go = GameObject.Find("DragGhostImage");
            if (go) dragGhostImage = go.GetComponent<Image>();
        }
        if (!itemThumb) itemThumb = GetComponentInChildren<Image>(true);
        cam = Camera.main;
    }

    // Esto lo usa CategoryItemsLoader al instanciar el botón
    public void SetItemPrefab(GameObject prefab) => itemPrefab = prefab;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!dragGhostImage || !itemThumb) return;
        dragGhostImage.sprite = itemThumb.sprite;
        dragGhostImage.rectTransform.sizeDelta = itemThumb.rectTransform.sizeDelta;
        dragGhostImage.transform.position = eventData.position;
        dragGhostImage.gameObject.SetActive(true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragGhostImage) dragGhostImage.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragGhostImage) dragGhostImage.gameObject.SetActive(false);
        if (!dropPlacer || !itemPrefab) return;

        // Intenta soltar en el piso usando la posición de pantalla al soltar
        if (dropPlacer.TryPlaceAtPointer(itemPrefab, eventData.position, out var spawned))
        {
            // opcional: feedback (anim, sonido, selección)
        }
    }
}
