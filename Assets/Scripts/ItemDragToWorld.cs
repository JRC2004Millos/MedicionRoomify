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
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Image itemThumb;

    Camera cam;

    void Awake()
    {
        if (!dropPlacer) dropPlacer = FindObjectOfType<RoomDropPlacer>();
        if (!dragGhostImage)
        {
            var go = GameObject.Find("DragGhostImage");
            if (go) dragGhostImage = go.GetComponent<Image>();
        }
        if (!itemThumb) itemThumb = GetComponentInChildren<Image>(true);
        cam = Camera.main;
    }

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

        if (dropPlacer.TryPlaceAtPointer(itemPrefab, eventData.position, out var spawned))
        {
        }
    }
}
