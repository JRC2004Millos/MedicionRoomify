using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FurnitureInteractionController : MonoBehaviour
{
    [Header("Setup")]
    public Camera cam;
    public LayerMask furnitureMask;
    [Range(300, 1200)] public float longPressMs = 550f;

    [Range(5f, 100f)]
    public float touchMoveTolerancePx = 40f;

    [Header("UI")]
    public RectTransform contextMenu;
    public Button btnMove;
    public Button btnDelete;
    public Button btnRotate;
    public Button btnCancel;

    private FurnitureInteractable current;

    private bool isPressing;
    private float pressStartTime;
    private Vector2 pressStartPos;
    private int activeFingerId = -1;
    private bool longPressTriggered;

    void Awake()
    {
        if (!cam) cam = Camera.main;

        if (furnitureMask.value == 0)
            furnitureMask = LayerMask.GetMask("Furniture");

        if (contextMenu)
            contextMenu.gameObject.SetActive(false);

        if (btnMove) btnMove.onClick.AddListener(OnMove);
        if (btnDelete) btnDelete.onClick.AddListener(OnDelete);
        if (btnRotate) btnRotate.onClick.AddListener(OnRotate);
        if (btnCancel) btnCancel.onClick.AddListener(OnCancel);
    }

    void Update()
    {
        if (Input.touchSupported && Application.isMobilePlatform)
        {
            HandleTouchInput();
        }
        else
        {
            HandleMouseInput();
        }
    }

    #region INPUT MOUSE (Editor / PC)
    void HandleMouseInput()
    {
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            isPressing = true;
            longPressTriggered = false;
            pressStartTime = Time.unscaledTime;
            pressStartPos = Input.mousePosition;
        }

        if (isPressing && !longPressTriggered)
        {
            float dtMs = (Time.unscaledTime - pressStartTime) * 1000f;
            float dist = Vector2.Distance(pressStartPos, Input.mousePosition);

            if (dist > touchMoveTolerancePx)
            {
                isPressing = false;
                return;
            }

            if (dtMs >= longPressMs)
            {
                longPressTriggered = true;
                TrySelectAtScreenPos(Input.mousePosition);
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            isPressing = false;
            longPressTriggered = false;
        }
    }
    #endregion

    void HandleTouchInput()
    {
        if (Input.touchCount == 0)
        {
            isPressing = false;
            longPressTriggered = false;
            activeFingerId = -1;
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);

            if (activeFingerId == -1 && t.phase == TouchPhase.Began)
            {
                if (EventSystem.current != null &&
                    EventSystem.current.IsPointerOverGameObject())
                {
                    continue;
                }

                activeFingerId = t.fingerId;
                isPressing = true;
                longPressTriggered = false;
                pressStartTime = Time.unscaledTime;
                pressStartPos = t.position;
                continue;
            }

            if (t.fingerId != activeFingerId)
                continue;

            float dtMs = (Time.unscaledTime - pressStartTime) * 1000f;
            float dist = Vector2.Distance(pressStartPos, t.position);

            switch (t.phase)
            {
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    {
                        if (!isPressing || longPressTriggered)
                            break;

                        if (dist > touchMoveTolerancePx)
                        {
                            isPressing = false;
                            activeFingerId = -1;
                            break;
                        }

                        if (dtMs >= longPressMs)
                        {
                            longPressTriggered = true;
                            TrySelectAtScreenPos(t.position);
                        }
                        break;
                    }

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    {
                        isPressing = false;
                        longPressTriggered = false;
                        activeFingerId = -1;
                        break;
                    }
            }
        }
    }



    #region SELECCIÓN Y MENÚ
    void TrySelectAtScreenPos(Vector2 screenPos)
    {
        if (!cam) return;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, furnitureMask))
        {
            var fi = hit.collider.GetComponentInParent<FurnitureInteractable>();
            if (fi != null)
            {
                SelectFurniture(fi, screenPos);
            }
        }
    }

    void SelectFurniture(FurnitureInteractable fi, Vector2 screenPos)
    {
        if (current != null)
            current.SetHighlight(false);

        current = fi;
        current.SetHighlight(true);

        ShowContextMenu(screenPos);
    }

    void ShowContextMenu(Vector2 screenPos)
    {
        if (!contextMenu) return;

        contextMenu.gameObject.SetActive(true);

        Vector2 localPos;
        RectTransform canvasRect = contextMenu.GetComponentInParent<Canvas>().GetComponent<RectTransform>();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            canvasRect.GetComponentInParent<Canvas>().worldCamera,
            out localPos
        );

        contextMenu.anchoredPosition = localPos;
    }

    void HideContextMenu()
    {
        if (contextMenu)
            contextMenu.gameObject.SetActive(false);
    }
    #endregion

    #region BOTONES DEL MENÚ
    void OnMove()
    {
        if (current == null) return;

        var gizmo = current.gameObject.AddComponent<FurnitureMoveGizmo>();
        gizmo.gridSnap = 0.05f;
        gizmo.useSnap = true;
        gizmo.allowRotate = true;

        gizmo.Begin(cam, (fi) =>
        {
            SaveFurniturePose(fi);
        });


        HideContextMenu();
    }

    void OnDelete()
    {
        if (current == null) return;

        string id = current.furnitureId;
        Destroy(current.gameObject);
        SaveFurnitureDeletion(id);

        current = null;
        HideContextMenu();
    }

    void OnRotate()
    {
        if (current == null) return;

        var gizmo = current.gameObject.AddComponent<FurnitureMoveGizmo>();
        gizmo.allowRotate = true;

        gizmo.BeginRotation(cam, (fi) =>
        {
            SaveFurniturePose(fi);
        });

        HideContextMenu();
    }

    void OnCancel()
    {
        if (current != null)
            current.SetHighlight(false);

        current = null;
        HideContextMenu();
    }
    #endregion

    #region PERSISTENCIA (stubs)
    void SaveFurniturePose(FurnitureInteractable fi)
    {
    }

    void SaveFurnitureDeletion(string id)
    {
    }
    #endregion
}
