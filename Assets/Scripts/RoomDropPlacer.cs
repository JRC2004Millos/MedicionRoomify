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
    [SerializeField] bool useRealisticScale = true;   // activa escalado realista por categoría

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

        // 1) Intersección pantalla → plano de piso
        var floorY = roomSpace.floorY;
        var plane = new Plane(Vector3.up, new Vector3(0f, floorY, 0f));
        var ray = sceneCamera.ScreenPointToRay(screenPos);

        if (!plane.Raycast(ray, out float dist))
            return false;

        var worldPos = ray.GetPoint(dist);

        // 2) Clamp a bounds (XZ)
        if (clampToBounds)
            worldPos = roomSpace.ClampWorldToInside(worldPos);

        // 3) Instanciar bajo RoomRoot
        spawned = Instantiate(prefab, parentRoot ? parentRoot : transform);
        spawned.transform.position = worldPos;

        // 4) Rotación “saludable”
        Quaternion rot = Quaternion.identity;

        // a) rumbo (yaw): cámara o cuarto
        Vector3 fwd = alignToRoomForward ? roomSpace.transform.forward : sceneCamera.transform.forward;
        fwd.y = 0f; if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();

        float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        if (yawSnapDeg > 0.01f) yaw = Mathf.Round(yaw / yawSnapDeg) * yawSnapDeg;

        rot = Quaternion.Euler(0f, yaw, 0f);

        // b) “de pie”
        if (keepUpright)
        {
            // zereamos pitch/roll manteniendo yaw
            Vector3 e = rot.eulerAngles;
            rot = Quaternion.Euler(0f, e.y, 0f);
        }

        // c) offset por modelos .OBJ (si vienen acostados 90°)
        if (rotationOffsetEuler != Vector3.zero)
            rot = rot * Quaternion.Euler(rotationOffsetEuler);

        spawned.transform.rotation = rot;

        if (useRealisticScale)
            ScaleToTargetHeight(spawned, GetTargetHeightM(spawned.name)); // por nombre prefijo/categoría

        if (autoScale)
            AutoScaleToRoom(spawned); // tu límite de huella del cuarto (déjalo)


        // 5) Apoyar base exacta al piso (usa Renderer.bounds)
        AlignBaseToFloor(spawned, floorY);

        // 6) Snap opcional
        if (enableSnap)
            spawned.transform.position = SnapXZ(spawned.transform.position, gridSnap);


        // 7) Empujar fuera de paredes si quedó tocándolas
        if (ResolveWallPenetration(spawned, wallMask))
        {
            // Reafirma Y exacta sobre el piso, por si el empuje movió algo verticalmente
            AlignBaseToFloor(spawned, floorY);
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

        // Calcular el bounding box total
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        // Cuánto mover en Y para que la base toque el piso
        float delta = floorY - b.min.y;

        // Mover el objeto entero
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
        // si no tiene nada, devuelve un bounds vacío centrado en el objeto
        return new Bounds(go.transform.position, Vector3.zero);
    }

    /* 1) Ajusta altura a un rango razonable (p.ej. 0.25–2.2 m)
       2) Limita la huella (X/Z) para no ocupar más de un % del cuarto */
    void AutoScaleToRoom(GameObject go)
    {
        // --- Paso A: normalizar altura ---
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
                b = GetCombinedBounds(go); // recomputar
            }
        }

        // --- Paso B: limitar huella al % del cuarto ---
        // dimensiones del cuarto (en mundo) desde RoomSpace
        float roomW = Mathf.Abs(roomSpace.maxX - roomSpace.minX);
        float roomL = Mathf.Abs(roomSpace.maxZ - roomSpace.minZ);

        float maxW = roomW * maxFootprintPct;
        float maxL = roomL * maxFootprintPct;

        // tamaño actual del objeto
        float w = b.size.x;
        float l = b.size.z;

        // Si excede en X o Z, escalar uniformemente para que quepa
        float sX = (w > 1e-4f) ? (maxW / w) : 1f;
        float sZ = (l > 1e-4f) ? (maxL / l) : 1f;

        float s = Mathf.Min(1f, sX, sZ); // solo reducimos, no agrandamos
        if (s < 0.999f)
        {
            go.transform.localScale *= s;
            // No hace falta recomputar bounds aquí; AlignBaseToFloor lo hará luego
        }
    }

    // Altura objetivo por tipo (m). Usa keywords en español/inglés.
    float GetTargetHeightM(string prefabName)
    {
        string n = prefabName.ToLowerInvariant();

        // Silla
        if (n.Contains("silla") || n.Contains("chair"))
            return 0.9f; // alto total aprox. respaldo

        // Mesa/escritorio
        if (n.Contains("mesa") || n.Contains("table") || n.Contains("desk"))
            return 0.75f;

        // Sofá
        if (n.Contains("sofa") || n.Contains("sofá") || n.Contains("couch"))
            return 0.85f;

        // Monitor (con base)
        if (n.Contains("monitor") || n.Contains("screen"))
            return 0.45f;

        // Teclado/mouse (bajitos para no desaparecer)
        if (n.Contains("keyboard") || n.Contains("teclado"))
            return 0.04f;
        if (n.Contains("mouse"))
            return 0.04f;

        // Lámpara: si dice “desk” asumimos de escritorio, si no, de piso
        if (n.Contains("lamp") || n.Contains("lampara") || n.Contains("lámpara"))
            return n.Contains("desk") ? 0.5f : 1.6f;

        // PC torre/laptop (altura dominante de la pieza)
        if (n.Contains("pc") || n.Contains("computer"))
            return 0.45f;
        if (n.Contains("laptop"))
            return 0.025f;

        // Genérico muebles
        if (n.Contains("furniture"))
            return 0.8f;

        // fallback
        return Mathf.Clamp(maxHeightM * 0.6f, 0.4f, 1.2f);
    }

    // Escala uniforme para que la altura del bounds = targetHeight (mantiene proporciones)
    void ScaleToTargetHeight(GameObject go, float targetHeightM)
    {
        var b = GetCombinedBounds(go);  // tu helper que ya calcula bounds por Renderers/Colliders
        if (b.size.y < 1e-4f) return;

        float s = targetHeightM / b.size.y;
        // evita escalas absurdas (x1000 / x0.001)
        s = Mathf.Clamp(s, 0.02f, 50f);

        if (Mathf.Abs(s - 1f) > 1e-3f)
        {
            go.transform.localScale *= s;
            // opcional: recomputar bounds si luego necesitas ajustes adicionales
            // b = GetCombinedBounds(go);
        }
    }

    bool ResolveWallPenetration(GameObject go, LayerMask wallsMask, float extra = 0.002f, int maxIters = 5)
    {
        // Asegúrate de que lo que colocas tenga al menos un Collider;
        // si no, puedes añadir temporalmente un BoxCollider con los bounds.
        var myCols = go.GetComponentsInChildren<Collider>();
        if (myCols == null || myCols.Length == 0) return false;

        bool moved = false;

        for (int iter = 0; iter < maxIters; iter++)
        {
            bool anyPenetration = false;

            foreach (var myCol in myCols)
            {
                // Volumen de búsqueda aproximado del collider
                var b = myCol.bounds;
                var hits = Physics.OverlapBox(
                    b.center, b.extents + Vector3.one * 0.001f,
                    go.transform.rotation, wallsMask,
                    QueryTriggerInteraction.Ignore);

                foreach (var other in hits)
                {
                    // Evitar contarse a sí mismo
                    if (other.transform.root == go.transform.root) continue;

                    if (Physics.ComputePenetration(
                        myCol, myCol.transform.position, myCol.transform.rotation,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 dir, out float dist))
                    {
                        // Mover en la dirección mínima que elimina la intersección
                        go.transform.position += dir * (dist + extra);
                        moved = true;
                        anyPenetration = true;
                    }
                }
            }

            if (!anyPenetration) break; // ya no está penetrando nada
        }

        return moved;
    }
}
