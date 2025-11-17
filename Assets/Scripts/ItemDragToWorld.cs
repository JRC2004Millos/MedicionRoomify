using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ItemDragToWorld : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("World drop")]
    [SerializeField] private RoomDropPlacer dropPlacer;

    [Header("Item")]
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private Image itemThumb;

    private Camera cam;

    void Awake()
    {
        if (!dropPlacer)
            dropPlacer = FindObjectOfType<RoomDropPlacer>();

        if (!itemThumb)
            itemThumb = GetComponentInChildren<Image>(true);

        cam = Camera.main;
    }

    public void SetItemPrefab(GameObject prefab) => itemPrefab = prefab;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!dropPlacer || !itemPrefab)
            return;
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dropPlacer || !itemPrefab)
            return;

        if (dropPlacer.TryPlaceAtPointer(itemPrefab, eventData.position, out var spawned))
        {
        }
    }
}