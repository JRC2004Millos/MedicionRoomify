using UnityEngine;

public class RoomDropPlacer : MonoBehaviour
{
    public Camera sceneCamera;       // arrastra la Main Camera
    public RoomSpace roomSpace;      // arrastra el RoomSpace de tu escena
    public Transform parentRoot;     // normalmente = roomSpace.roomRoot
    public float gridSnap = 0.10f;   // opcional: snap a 10cm
    public bool enableSnap = true;

    void Reset()
    {
        if (sceneCamera == null) sceneCamera = Camera.main;
        if (roomSpace == null) roomSpace = FindFirstObjectByType<RoomSpace>();
        if (roomSpace != null) parentRoot = roomSpace.roomRoot;
    }

    public bool TryPlaceAtPointer(GameObject prefab, Vector2 screenPos, out GameObject spawned)
    {
        spawned = null;
        if (prefab == null || sceneCamera == null || roomSpace == null) return false;

        // Raycast contra un plano horizontal a la altura del piso
        Plane floorPlane = new Plane(Vector3.up, new Vector3(0, roomSpace.floorY, 0));
        Ray ray = sceneCamera.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0));
        if (!floorPlane.Raycast(ray, out float enter)) return false;

        Vector3 hit = ray.GetPoint(enter);
        if (enableSnap) hit = SnapXZ(hit, gridSnap);
        hit = roomSpace.ClampWorldToInside(hit);

        spawned = Instantiate(prefab, hit, Quaternion.identity, parentRoot);
        AlignBaseToFloor(spawned, roomSpace.floorY);
        return true;
    }

    private static Vector3 SnapXZ(Vector3 p, float step)
    {
        if (step <= 0f) return p;
        p.x = Mathf.Round(p.x / step) * step;
        p.z = Mathf.Round(p.z / step) * step;
        return p;
    }

    public static void AlignBaseToFloor(GameObject go, float floorY)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        float delta = floorY - b.min.y;
        go.transform.position += new Vector3(0, delta, 0);
    }
}
