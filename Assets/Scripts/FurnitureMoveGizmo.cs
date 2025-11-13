using UnityEngine;
using System;

[RequireComponent(typeof(FurnitureInteractable))]
public class FurnitureMoveGizmo : MonoBehaviour
{
    public float gridSnap = 0.05f;     // 5 cm
    public bool useSnap = true;
    public bool allowRotate = true;
    public float rotateSpeed = 0.25f;  // grados por píxel (promedio)

    private FurnitureInteractable fi;
    private Camera cam;
    private Plane movePlane;
    private Vector3 offset;
    private bool moving = false;
    private bool rotating = false;
    private Action<FurnitureInteractable> onFinishCb;

    public void Begin(Camera c, Action<FurnitureInteractable> onFinish)
    {
        fi = GetComponent<FurnitureInteractable>();
        cam = c ? c : Camera.main;

        // plano horizontal a la altura actual del mueble
        float y = transform.position.y;
        movePlane = new Plane(Vector3.up, new Vector3(0f, y, 0f));

        moving = true;
        onFinishCb = onFinish;
    }

    // ---------------------------
    //  MODO ROTAR (Y)
    // ---------------------------
    public void BeginRotation(Camera c, Action<FurnitureInteractable> onFinish)
    {
        fi = GetComponent<FurnitureInteractable>();
        cam = c;
        onFinishCb = onFinish;

        moving = false;
        rotating = true;
    }

    void Update()
    {
        if (!moving) return;
        HandleTouch();
    }

    void HandleTouch()
    {
        if (Input.touchCount == 0) return;

        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);

            if (t.phase == TouchPhase.Began)
                offset = WorldPointOnPlane(t.position) - transform.position;

            if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
            {
                Vector3 target = WorldPointOnPlane(t.position) - offset;
                MoveTo(target);
            }
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                Finish();
        }
        else if (Input.touchCount == 2 && allowRotate)
        {
            Touch t1 = Input.GetTouch(0);
            Touch t2 = Input.GetTouch(1);
            float avgDx = (t1.deltaPosition.x + t2.deltaPosition.x) * 0.5f;
            transform.Rotate(Vector3.up, avgDx * rotateSpeed, Space.World);
        }
    }

    Vector3 WorldPointOnPlane(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (movePlane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);
        return transform.position;
    }

    void MoveTo(Vector3 target)
    {
        // Snap XZ
        if (useSnap)
        {
            target.x = Mathf.Round(target.x / gridSnap) * gridSnap;
            target.z = Mathf.Round(target.z / gridSnap) * gridSnap;
        }
        // Mantener altura (plano ya está a la Y del mueble)
        target.y = transform.position.y;

        // (Opcional) Clamp dentro del cuarto si usas RoomSpace:
        // target = RoomSpace.Instance.ClampWorldToInside(target);

        transform.position = target;
    }

    void Finish()
    {
        moving = false;
        onFinishCb?.Invoke(fi);
        Destroy(this); // componente efímero para cada edición
    }
}
