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
    public float touchMoveTolerancePx = 40f;   // tolerancia para que siga contando como "press"

    [Header("UI")]
    public RectTransform contextMenu;
    public Button btnMove;
    public Button btnDelete;
    public Button btnRotate;
    public Button btnCancel;

    private FurnitureInteractable current;

    // Estado del long-press
    private bool isPressing;
    private float pressStartTime;
    private Vector2 pressStartPos;
    private int activeFingerId = -1;
    private bool longPressTriggered;

    void Awake()
    {
        if (!cam) cam = Camera.main;

        // Si te olvidas de poner la máscara, que use "Furniture" por nombre
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
        // Priorizar touch en móvil
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
            return; // El click está sobre UI
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
                // Se movió demasiado = ya no cuenta como long-press
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
            // Reset si no hay toques
            isPressing = false;
            longPressTriggered = false;
            activeFingerId = -1;
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);

            // Si aún no tenemos dedo activo, tomamos uno cuando empiece
            if (activeFingerId == -1 && t.phase == TouchPhase.Began)
            {
                // Evitar si empezó sobre UI (Input System nuevo)
                if (EventSystem.current != null &&
                    EventSystem.current.IsPointerOverGameObject())
                {
                    // ignoramos este toque y seguimos con otros
                    continue;
                }

                activeFingerId = t.fingerId;
                isPressing = true;
                longPressTriggered = false;
                pressStartTime = Time.unscaledTime;
                pressStartPos = t.position;
                continue;
            }

            // Si este no es el dedo activo, lo ignoramos
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

                        // Si se mueve más de la tolerancia, cancelamos el long-press
                        if (dist > touchMoveTolerancePx)
                        {
                            isPressing = false;
                            activeFingerId = -1;
                            break;
                        }

                        // Aquí es donde realmente “holdear” dispara la selección
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
                        // Si levantó el dedo antes del long-press -> no hace nada
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
        // Quitamos highlight anterior
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

        // Asumimos canvas en Screen Space - Overlay
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
        // TODO: guardar fi.furnitureId + transform en tu JSON/estado
    }

    void SaveFurnitureDeletion(string id)
    {
        // TODO: eliminar id de tu JSON/estado
    }
    #endregion
}
