using UnityEngine;
using System;

[RequireComponent(typeof(FurnitureInteractable))]
public class FurnitureMoveGizmo : MonoBehaviour
{
    public float gridSnap = 0.05f;
    public bool useSnap = true;
    public bool allowRotate = true;
    public float rotateSpeed = 0.25f;

    private FurnitureInteractable fi;
    private Camera cam;
    private Plane movePlane;
    private Vector3 offset;
    private bool moving = false;
    private bool rotating = false;
    private Action<FurnitureInteractable> onFinishCb;
    private FreeCameraController camController;

    public void Begin(Camera c, Action<FurnitureInteractable> onFinish)
    {
        fi = GetComponent<FurnitureInteractable>();
        cam = c ? c : Camera.main;

        camController = cam ? cam.GetComponentInParent<FreeCameraController>() : null;
        if (camController != null)
            camController.SetInputEnabled(false);

        float y = transform.position.y;
        movePlane = new Plane(Vector3.up, new Vector3(0f, y, 0f));

        moving = true;
        onFinishCb = onFinish;
    }

    public void BeginRotation(Camera c, Action<FurnitureInteractable> onFinish)
    {
        fi = GetComponent<FurnitureInteractable>();
        cam = c;
        onFinishCb = onFinish;

        camController = cam ? cam.GetComponentInParent<FreeCameraController>() : null;
        if (camController != null)
            camController.SetInputEnabled(false);

        moving = false;
        rotating = true;
    }

    void Update()
    {
        if (moving)
            HandleTouchMove();

        if (rotating)
            HandleTouchRotate();
    }

    void HandleTouchMove()
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
        if (useSnap)
        {
            target.x = Mathf.Round(target.x / gridSnap) * gridSnap;
            target.z = Mathf.Round(target.z / gridSnap) * gridSnap;
        }
        target.y = transform.position.y;

        transform.position = target;
    }

    void Finish()
    {
        moving = false;
        onFinishCb?.Invoke(fi);

        if (camController != null)
            camController.SetInputEnabled(true);

        Destroy(this);
    }

    void HandleTouchRotate()
    {
        if (Input.touchCount < 2) return;

        Touch t1 = Input.GetTouch(0);
        Touch t2 = Input.GetTouch(1);

        float avgDx = (t1.deltaPosition.x + t2.deltaPosition.x) * 0.5f;
        transform.Rotate(Vector3.up, avgDx * rotateSpeed, Space.World);

        if (t1.phase == TouchPhase.Ended || t2.phase == TouchPhase.Ended)
            FinishRotation();
    }

    void FinishRotation()
    {
        rotating = false;
        onFinishCb?.Invoke(fi);

        if (camController != null)
            camController.SetInputEnabled(true);

        Destroy(this);
    }

    void OnDestroy()
    {
        if (camController != null)
            camController.SetInputEnabled(true);
    }
}
