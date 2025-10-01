// Assets/Scripts/FreeCameraController.cs
using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    [Header("Velocidades")]
    public float moveSpeed = 2.0f;
    public float fastMultiplier = 4.0f;
    public float lookSensitivity = 120f; // grados/seg

    [Header("Mobile")]
    public float panSpeedTouch = 1.2f;   // m/s
    public float pinchZoomSpeed = 2.0f;  // m/s

    float yaw, pitch;
    Vector3 lastPanPos1, lastPanPos2;
    bool rightMouseHeld;

    void Start()
    {
        var euler = transform.eulerAngles;
        yaw = euler.y; pitch = euler.x;
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

    void DesktopLook()
    {
        if (Input.GetMouseButtonDown(1)) rightMouseHeld = true;
        if (Input.GetMouseButtonUp(1)) rightMouseHeld = false;
        if (!rightMouseHeld) return;

        float dx = Input.GetAxis("Mouse X");
        float dy = -Input.GetAxis("Mouse Y");
        yaw += dx * lookSensitivity * Time.deltaTime;
        pitch += dy * lookSensitivity * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, -85f, 85f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void DesktopMove()
    {
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
        Vector3 dir = new Vector3(
            (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
            (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0),
            (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)
        );
        transform.position += transform.TransformDirection(dir) * speed * Time.deltaTime;
    }

    void MobileInput()
    {
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

            // Pan (mover plano XZ) con dos dedos en la misma dirección
            if (t1.phase == TouchPhase.Moved && t2.phase == TouchPhase.Moved)
            {
                Vector2 avgDelta = 0.5f * (t1.deltaPosition + t2.deltaPosition);
                Vector3 right = transform.right; right.y = 0; right.Normalize();
                Vector3 forward = transform.forward; forward.y = 0; forward.Normalize();
                Vector3 move = (right * -avgDelta.x + forward * -avgDelta.y) * (panSpeedTouch / Screen.dpi);
                transform.position += move;
            }

            // Pinch = avanzar/retroceder
            if (t1.phase == TouchPhase.Moved || t2.phase == TouchPhase.Moved)
            {
                float prevDist = (t1.position - t1.deltaPosition - (t2.position - t2.deltaPosition)).magnitude;
                float currDist = (t1.position - t2.position).magnitude;
                float diff = currDist - prevDist;
                Vector3 del = transform.forward * (diff / Screen.dpi) * pinchZoomSpeed;
                transform.position += del;
            }
        }
    }
}
