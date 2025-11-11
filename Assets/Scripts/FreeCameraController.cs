// Assets/Scripts/FreeCameraController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FreeCameraController : MonoBehaviour
{
    [Header("Velocidades")]
    public float moveSpeed = 2.0f;
    public float fastMultiplier = 4.0f;
    public float lookSensitivity = 120f; // grados/seg

    [Header("Mobile")]
    public float panSpeedTouch = 1.2f;   // m/s
    public float pinchZoomSpeed = 2.0f;  // m/s

    [Header("Bloqueo por UI")]
    [Tooltip("Arrastra aquí el Viewport del ScrollView del catálogo y/o otros paneles UI que deban bloquear la cámara.")]
    public List<RectTransform> uiBlockAreas = new List<RectTransform>();
    [Tooltip("Si tu Canvas es Screen Space - Camera, pon aquí esa cámara. Si lo dejas vacío usa Camera.main.")]
    public Camera uiCamera;

    float yaw, pitch;
    bool rightMouseHeld;
    bool blockFromUIUntilAllReleased = false;

    void Start()
    {
        var euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
        if (Camera.main) Camera.main.nearClipPlane = 0.01f;
    }

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        DesktopLook();
        DesktopMove();
#elif UNITY_ANDROID || UNITY_IOS
        MobileInput();
#endif
    }

    // ------------------- PC -------------------
    void DesktopLook()
    {
        // no iniciar rotación si el click derecho comenzó sobre UI
        if (Input.GetMouseButtonDown(1) && PointerOverUIAt(Input.mousePosition)) return;

        if (Input.GetMouseButtonDown(1)) rightMouseHeld = true;
        if (Input.GetMouseButtonUp(1)) rightMouseHeld = false;

        if (!rightMouseHeld) return;
        if (PointerOverUIAt(Input.mousePosition)) return;

        float dx = Input.GetAxis("Mouse X");
        float dy = -Input.GetAxis("Mouse Y");
        yaw += dx * lookSensitivity * Time.deltaTime;
        pitch += dy * lookSensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, -85f, 85f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void DesktopMove()
    {
        // opcional: bloquea WASD si el puntero está sobre UI
        if (PointerOverUIAt(Input.mousePosition)) return;

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
        Vector3 dir = new Vector3(
            (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
            (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0),
            (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)
        );
        transform.position += transform.TransformDirection(dir) * speed * Time.deltaTime;
    }

    // ------------------- MOBILE -------------------
    void MobileInput()
    {
        // Si algún toque empieza sobre UI -> bloquea hasta que todos se levanten
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && PointerOverUIAt(t.position))
                {
                    blockFromUIUntilAllReleased = true;
                    break;
                }
            }

            if (blockFromUIUntilAllReleased)
            {
                bool anyActive = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var p = Input.GetTouch(i).phase;
                    if (p != TouchPhase.Canceled && p != TouchPhase.Ended)
                    {
                        anyActive = true;
                        break;
                    }
                }
                if (!anyActive) blockFromUIUntilAllReleased = false;
                return; // 🔒 no mover cámara mientras el gesto pertenece a UI
            }
        }

        if (Input.touchCount == 1)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Moved)
            {
                Vector2 d = t.deltaPosition;
                yaw += (d.x / Screen.width) * lookSensitivity;
                pitch -= (d.y / Screen.height) * lookSensitivity;
                pitch = Mathf.Clamp(pitch, -85f, 85f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
        }
        else if (Input.touchCount == 2)
        {
            var t1 = Input.GetTouch(0);
            var t2 = Input.GetTouch(1);

            // Pan con dos dedos
            if (t1.phase == TouchPhase.Moved && t2.phase == TouchPhase.Moved)
            {
                Vector2 avgDelta = 0.5f * (t1.deltaPosition + t2.deltaPosition);
                Vector3 right = transform.right; right.y = 0; right.Normalize();
                Vector3 forward = transform.forward; forward.y = 0; forward.Normalize();
                Vector3 move = (right * -avgDelta.x + forward * -avgDelta.y) * (panSpeedTouch / Mathf.Max(1f, Screen.dpi));
                transform.position += move;
            }

            // Pinch zoom
            if (t1.phase == TouchPhase.Moved || t2.phase == TouchPhase.Moved)
            {
                float prevDist = (t1.position - t1.deltaPosition - (t2.position - t2.deltaPosition)).magnitude;
                float currDist = (t1.position - t2.position).magnitude;
                float diff = currDist - prevDist;
                Vector3 del = transform.forward * (diff / Mathf.Max(1f, Screen.dpi)) * pinchZoomSpeed;
                transform.position += del;
            }
        }
    }

    // ------------------- UI HELPERS -------------------
    bool PointerOverUIAt(Vector2 screenPos)
    {
        // 1) EventSystem (si existe)
        if (EventSystem.current != null)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (EventSystem.current.IsPointerOverGameObject()) return true;
#else
            for (int i = 0; i < Input.touchCount; i++)
                if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
                    return true;
#endif
            // Raycast manual
            var data = new PointerEventData(EventSystem.current) { position = screenPos };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);
            if (results.Count > 0) return true;
        }

        // 2) Chequeo geométrico contra paneles asignados (robusto)
        if (uiBlockAreas != null)
        {
            Camera cam = uiCamera != null ? uiCamera : Camera.main;
            for (int i = 0; i < uiBlockAreas.Count; i++)
            {
                var rt = uiBlockAreas[i];
                if (rt == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam))
                    return true;
            }
        }
        return false;
    }
}
