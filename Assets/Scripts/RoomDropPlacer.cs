using UnityEngine;

public class RoomDropPlacer : MonoBehaviour
{

    [Header("Scene references")]
    [SerializeField] Camera sceneCamera;
    [SerializeField] RoomSpace roomSpace;
    [SerializeField] Transform parentRoot;

    [Header("Wall collision settings")]
    [SerializeField] LayerMask wallMask;

    [Header("Placement options")]
    [SerializeField] bool clampToBounds = true;
    [SerializeField] bool enableSnap = false;
    [SerializeField] float gridSnap = 0.25f;

    [Header("Rotation")]
    [SerializeField] bool keepUpright = true;
    [SerializeField] bool alignToRoomForward = true;
    [SerializeField] float yawSnapDeg = 0f;
    [SerializeField] Vector3 rotationOffsetEuler = Vector3.zero;

    [Header("Auto scale")]
    [SerializeField] bool autoScale = true;
    [SerializeField] float minHeightM = 0.25f;
    [SerializeField] float maxHeightM = 2.2f;
    [SerializeField] float maxFootprintPct = 0.45f;

    [Header("Real-world scale")]
    [SerializeField] bool useRealisticScale = true;

    void Reset()
    {
        if (sceneCamera == null) sceneCamera = Camera.main;
        if (roomSpace == null) roomSpace = FindFirstObjectByType<RoomSpace>();
        if (roomSpace != null) parentRoot = roomSpace.roomRoot;
    }

    public bool TryPlaceAtPointer(GameObject prefab, Vector2 screenPos, out GameObject spawned)
    {
        spawned = null;
        if (!sceneCamera || !roomSpace || !prefab) return false;

        var floorY = roomSpace.floorY;
        var plane = new Plane(Vector3.up, new Vector3(0f, floorY, 0f));
        var ray = sceneCamera.ScreenPointToRay(screenPos);

        if (!plane.Raycast(ray, out float dist))
            return false;

        var worldPos = ray.GetPoint(dist);

        if (clampToBounds)
            worldPos = roomSpace.ClampWorldToInside(worldPos);

        spawned = Instantiate(prefab, parentRoot ? parentRoot : transform);
        spawned.transform.position = worldPos;

        Quaternion rot = Quaternion.identity;

        Vector3 fwd = alignToRoomForward ? roomSpace.transform.forward : sceneCamera.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();

        float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        if (yawSnapDeg > 0.01f) yaw = Mathf.Round(yaw / yawSnapDeg) * yawSnapDeg;

        rot = Quaternion.Euler(0f, yaw, 0f);

        if (keepUpright)
        {
            Vector3 e = rot.eulerAngles;
            rot = Quaternion.Euler(0f, e.y, 0f);
        }

        if (rotationOffsetEuler != Vector3.zero)
            rot = rot * Quaternion.Euler(rotationOffsetEuler);

        spawned.transform.rotation = rot;

        if (useRealisticScale)
            ScaleToTargetHeight(spawned, GetTargetHeightM(spawned.name));

        if (autoScale)
            AutoScaleToRoom(spawned);

        EnsureCollider(spawned);
        if (!spawned.GetComponentInChildren<Collider>())
            Debug.LogWarning($"Sin collider: {spawned.name}");

        AlignBaseToFloor(spawned, floorY);

        if (enableSnap)
            spawned.transform.position = SnapXZ(spawned.transform.position, gridSnap);

        if (ResolveWallPenetration(spawned, wallMask, 0.05f, 15))
        {
            Debug.Log($"Empujado {spawned.name} fuera de pared");
            spawned.transform.position = roomSpace.ClampWorldToInside(spawned.transform.position);
            AlignBaseToFloor(spawned, floorY);
        }

        {
            int furnLayer = LayerMask.NameToLayer("Furniture");
            if (furnLayer >= 0)
            {
                foreach (Transform t in spawned.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = furnLayer;
            }

            var fi = spawned.GetComponent<FurnitureInteractable>();
            if (!fi)
                fi = spawned.gameObject.AddComponent<FurnitureInteractable>();

            if (string.IsNullOrEmpty(fi.furnitureId))
                fi.furnitureId = prefab.name;

            var existingColliders = spawned.GetComponentsInChildren<Collider>(true);
            if (existingColliders == null || existingColliders.Length == 0)
            {
                Renderer rend = spawned.GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    Bounds b = rend.bounds;

                    BoxCollider box = spawned.AddComponent<BoxCollider>();

                    box.center = spawned.transform.InverseTransformPoint(b.center);

                    Vector3 sizeWorld = b.size;
                    Vector3 lossy = spawned.transform.lossyScale;
                    Vector3 sizeLocal = new Vector3(
                        lossy.x != 0 ? sizeWorld.x / lossy.x : sizeWorld.x,
                        lossy.y != 0 ? sizeWorld.y / lossy.y : sizeWorld.y,
                        lossy.z != 0 ? sizeWorld.z / lossy.z : sizeWorld.z
                    );
                    box.size = sizeLocal;
                }
                else
                {
                    spawned.AddComponent<BoxCollider>();
                }
            }
        }


        return true;
    }

    private static Vector3 SnapXZ(Vector3 p, float step)
    {
        if (step <= 0f) return p;
        p.x = Mathf.Round(p.x / step) * step;
        p.z = Mathf.Round(p.z / step) * step;
        return p;
    }

    void AlignBaseToFloor(GameObject go, float floorY)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        float delta = floorY - b.min.y;

        if (Mathf.Abs(delta) > 0.0001f)
            go.transform.position += new Vector3(0f, delta, 0f);
    }

    Bounds GetCombinedBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends != null && rends.Length > 0)
        {
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }
        var cols = go.GetComponentsInChildren<Collider>(true);
        if (cols != null && cols.Length > 0)
        {
            var b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            return b;
        }
        return new Bounds(go.transform.position, Vector3.zero);
    }

    void AutoScaleToRoom(GameObject go)
    {
        var b = GetCombinedBounds(go);
        if (b.size.y > 1e-4f)
        {
            float h = b.size.y;
            float scale = 1f;

            if (h < minHeightM) scale = minHeightM / h;
            else if (h > maxHeightM) scale = maxHeightM / h;

            if (Mathf.Abs(scale - 1f) > 1e-3f)
            {
                go.transform.localScale *= scale;
                b = GetCombinedBounds(go);
            }
        }

        float roomW = Mathf.Abs(roomSpace.maxX - roomSpace.minX);
        float roomL = Mathf.Abs(roomSpace.maxZ - roomSpace.minZ);

        float maxW = roomW * maxFootprintPct;
        float maxL = roomL * maxFootprintPct;

        float w = b.size.x;
        float l = b.size.z;

        float sX = (w > 1e-4f) ? (maxW / w) : 1f;
        float sZ = (l > 1e-4f) ? (maxL / l) : 1f;

        float s = Mathf.Min(1f, sX, sZ);
        if (s < 0.999f)
        {
            go.transform.localScale *= s;
        }
    }

    float GetTargetHeightM(string prefabName)
    {
        string n = prefabName.ToLowerInvariant();

        if (n.Contains("silla") || n.Contains("chair"))
            return 0.9f;

        if (n.Contains("mesa") || n.Contains("table") || n.Contains("desk"))
            return 0.75f;

        if (n.Contains("sofa") || n.Contains("sofá") || n.Contains("couch"))
            return 0.85f;

        if (n.Contains("monitor") || n.Contains("screen"))
            return 0.45f;

        if (n.Contains("keyboard") || n.Contains("teclado"))
            return 0.04f;
        if (n.Contains("mouse"))
            return 0.04f;

        if (n.Contains("lamp") || n.Contains("lampara") || n.Contains("lámpara"))
            return n.Contains("desk") ? 0.5f : 1.6f;

        if (n.Contains("pc") || n.Contains("computer"))
            return 0.45f;
        if (n.Contains("laptop"))
            return 0.025f;

        if (n.Contains("furniture"))
            return 0.8f;

        return Mathf.Clamp(maxHeightM * 0.6f, 0.4f, 1.2f);
    }

    void ScaleToTargetHeight(GameObject go, float targetHeightM)
    {
        var b = GetCombinedBounds(go);
        if (b.size.y < 1e-4f) return;

        float s = targetHeightM / b.size.y;
        s = Mathf.Clamp(s, 0.02f, 50f);

        if (Mathf.Abs(s - 1f) > 1e-3f)
        {
            go.transform.localScale *= s;
        }
    }

    bool ResolveWallPenetration(GameObject go, LayerMask wallsMask, float extra = 0.002f, int maxIters = 5)
    {
        var myCols = go.GetComponentsInChildren<Collider>();
        if (myCols == null || myCols.Length == 0) return false;

        bool moved = false;

        for (int iter = 0; iter < maxIters; iter++)
        {
            bool anyPenetration = false;

            foreach (var myCol in myCols)
            {
                var b = myCol.bounds;
                var hits = Physics.OverlapBox(
                    b.center,
                    b.extents + Vector3.one * 0.001f,
                    Quaternion.identity,
                    wallsMask,
                    QueryTriggerInteraction.Ignore
                );

                foreach (var other in hits)
                {
                    if (other.transform.root == go.transform.root) continue;

                    if (Physics.ComputePenetration(
                        myCol, myCol.transform.position, myCol.transform.rotation,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 dir, out float dist))
                    {
                        go.transform.position += dir * (dist + extra);
                        moved = true;
                        anyPenetration = true;
                    }
                }
            }

            if (!anyPenetration) break;
        }

        return moved;
    }

    void EnsureCollider(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>()) return;

        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) { go.AddComponent<BoxCollider>(); return; }

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        var bc = go.AddComponent<BoxCollider>();
        bc.center = go.transform.InverseTransformPoint(b.center);
        Vector3 sx = go.transform.InverseTransformVector(new Vector3(b.size.x, 0, 0));
        Vector3 sy = go.transform.InverseTransformVector(new Vector3(0, b.size.y, 0));
        Vector3 sz = go.transform.InverseTransformVector(new Vector3(0, 0, b.size.z));
        bc.size = new Vector3(Mathf.Abs(sx.x) + Mathf.Abs(sx.y) + Mathf.Abs(sx.z),
                              Mathf.Abs(sy.x) + Mathf.Abs(sy.y) + Mathf.Abs(sy.z),
                              Mathf.Abs(sz.x) + Mathf.Abs(sz.y) + Mathf.Abs(sz.z));
    }
}
