using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;

[RequireComponent(typeof(CharacterController))]
public class FreeCameraController : MonoBehaviour
{
    [Header("Velocidades")]
    public float moveSpeed = 2.0f;
    public float fastMultiplier = 4.0f;
    public float lookSensitivity = 120f; // grados/seg

    [Header("Mobile")]
    public float panSpeedTouch = 1.2f;   // m/s
    public float pinchZoomSpeed = 2.0f;  // m/s

    [Header("Alturas (clamp vertical simple)")]
    [Tooltip("Altura de ojos respecto al piso.")]
    public float eyeHeight = 1.65f;
    [Tooltip("Margen para no tocar el techo.")]
    public float headClearance = 0.05f;
    [Tooltip("Altura del cuarto (fallback si no se detecta).")]
    public float roomHeightFallback = 2.6f;

    [Header("Bloqueo por UI")]
    public List<RectTransform> uiBlockAreas = new List<RectTransform>();
    public Camera uiCamera;

    float yaw, pitch;
    bool rightMouseHeld;
    bool blockFromUIUntilAllReleased = false;

    CharacterController cc;
    float floorY = 0f;         // se actualizará si existe RoomBuilder
    float roomHeight = 2.6f;   // idem

    [Header("Spawn dentro del cuarto")]
    public bool snapInsideOnStart = true;
    public float spawnFromTop = 5f;    // metros por encima del piso para el raycast
    public float spawnMargin = 0.1f;   // margen alejado de bordes

    [Header("TP forzado (opcional)")]
    public Transform forcedSpawn;   // <- arrástrale un Empty dentro del cuarto
    public bool snapToFloorAtSpawn = true; // ajusta Y al piso si hay MeshCollider

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        // Capsule “parada”: radio pequeño para pasar por esquinas sin atascar
        cc.radius = 0.2f;
        cc.height = Mathf.Max(eyeHeight, 0.5f);
        cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
        cc.stepOffset = 0.3f;   // subir pequeños bordes
        cc.minMoveDistance = 0f;
        cc.enableOverlapRecovery = true;
    }

    void Start()
    {
        var euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
        if (Camera.main) Camera.main.nearClipPlane = 0.01f;

        // Intentar leer altura real del cuarto de tu RoomBuilder
        var rb = FindFirstObjectByType<RoomBuilder>();
        if (rb != null)
        {
            // En tu RoomBuilder guarda floorBaseY como público o internal con [SerializeField]
            // (del ajuste anterior donde alineaste paredes)
            var floorBaseYField = typeof(RoomBuilder).GetField("floorBaseY",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (floorBaseYField != null)
                floorY = (float)floorBaseYField.GetValue(rb);

            // room_dimensions?.height
            var dataField = typeof(RoomBuilder).GetField("data",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (dataField != null)
            {
                var data = dataField.GetValue(rb);
                if (data != null)
                {
                    var dims = data.GetType().GetField("room_dimensions");
                    if (dims != null)
                    {
                        var d = dims.GetValue(data);
                        if (d != null)
                        {
                            var hF = d.GetType().GetField("height");
                            if (hF != null) roomHeight = Mathf.Max(0.5f, (float)hF.GetValue(d));
                        }
                    }
                }
            }
        }
        if (roomHeight <= 0.51f) roomHeight = roomHeightFallback;

        // coloca la cámara a la altura de ojo dentro del cuarto
        Vector3 p = transform.position;
        p.y = floorY + eyeHeight;
        transform.position = p;

        if (snapInsideOnStart) SnapInsideRoom();

    }

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        DesktopLook();
        DesktopMove();
#elif UNITY_ANDROID || UNITY_IOS
        MobileInput();
#endif
        ClampVerticalInsideRoom();
    }

    // ------------------- PC -------------------
    void DesktopLook()
    {
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
        if (PointerOverUIAt(Input.mousePosition)) return;

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
        Vector3 dir = new Vector3(
            (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
            (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0),
            (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)
        );
        Vector3 worldDelta = transform.TransformDirection(dir) * speed * Time.deltaTime;
        cc.Move(worldDelta); // ✅ respeta colisiones con paredes/techo
    }

    // ------------------- MOBILE -------------------
    void MobileInput()
    {
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
                    if (p != TouchPhase.Canceled && p != TouchPhase.Ended) { anyActive = true; break; }
                }
                if (!anyActive) blockFromUIUntilAllReleased = false;
                return;
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
                Vector3 move = (right * -avgDelta.x + forward * -avgDelta.y)
                               * (panSpeedTouch / Mathf.Max(1f, Screen.dpi));
                cc.Move(move);
            }

            // Pinch zoom
            if (t1.phase == TouchPhase.Moved || t2.phase == TouchPhase.Moved)
            {
                float prevDist = (t1.position - t1.deltaPosition - (t2.position - t2.deltaPosition)).magnitude;
                float currDist = (t1.position - t2.position).magnitude;
                float diff = currDist - prevDist;
                Vector3 del = transform.forward * (diff / Mathf.Max(1f, Screen.dpi)) * pinchZoomSpeed;
                cc.Move(del);
            }
        }
    }

    // ------------------- LIMITE VERTICAL -------------------
    void ClampVerticalInsideRoom()
    {
        // Mantén siempre la cámara entre piso+ojos y techo-clearance, ignorando gravedad
        Vector3 p = transform.position;
        float minY = floorY + eyeHeight;
        float maxY = floorY + Mathf.Max(eyeHeight + 0.05f, roomHeight - headClearance);
        if (p.y < minY) p.y = minY;
        if (p.y > maxY) p.y = maxY;
        transform.position = p;
    }

    // ------------------- UI HELPERS -------------------
    bool PointerOverUIAt(Vector2 screenPos)
    {
        if (EventSystem.current != null)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (EventSystem.current.IsPointerOverGameObject()) return true;
#else
            for (int i = 0; i < Input.touchCount; i++)
                if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
#endif
            var data = new PointerEventData(EventSystem.current) { position = screenPos };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);
            if (results.Count > 0) return true;
        }

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

    void SnapInsideRoom()
    {
        // --- A) Si hay un TP forzado, úsalo y listo ---
        if (forcedSpawn != null)
        {
            Vector3 pos = forcedSpawn.position;

            if (snapToFloorAtSpawn)
            {
                // intenta ajustar Y exactamente al piso (si lo golpea)
                // para no depender de capas, probamos RaycastAll y elegimos el "Floor" si existe
                RaycastHit[] hits = Physics.RaycastAll(pos + Vector3.up * 3f, Vector3.down, 6f);
                float bestY = float.NegativeInfinity;
                MeshCollider floorCol = null;

                // Busca el collider del piso por nombre
                var floors = GameObject.FindObjectsOfType<MeshCollider>();
                foreach (var mc in floors)
                {
                    if (mc && mc.gameObject.name.ToLower().Contains("floor"))
                    {
                        floorCol = mc; break;
                    }
                }

                if (floorCol != null)
                {
                    for (int i = 0; i < hits.Length; i++)
                    {
                        if (hits[i].collider == floorCol)
                            bestY = Mathf.Max(bestY, hits[i].point.y);
                    }
                }
                else
                {
                    // si no localiza el floor concreto, acepta la superficie más alta que esté debajo
                    for (int i = 0; i < hits.Length; i++)
                        bestY = Mathf.Max(bestY, hits[i].point.y);
                }

                if (!float.IsNegativeInfinity(bestY))
                    pos.y = bestY + eyeHeight;
                else
                    pos.y = pos.y + eyeHeight; // fallback simple
            }
            else
            {
                pos.y += eyeHeight;
            }

            bool was = cc.enabled; cc.enabled = false;
            transform.position = pos;
            cc.enabled = was;

            // actualiza límites verticales si tienes RoomBuilder
            // actualiza límites verticales si tienes RoomBuilder
            var rbX = FindFirstObjectByType<RoomBuilder>();
            if (rbX != null)
            {
                floorY = rbX.FloorBaseY;
                roomHeight = rbX.RoomHeightMeters;
            }
            else
            {
                roomHeight = roomHeightFallback;
            }

            return; // 👈 ya hicimos TP, salimos
        }

        // --- B) Si NO hay TP forzado, puedes dejar aquí cualquiera de tus lógicas anteriores ---
        // (por simplicidad, coloca al centro del bounds del piso y ajusta a piso)
        MeshFilter floorMF = null;
        var allMF = GameObject.FindObjectsOfType<MeshFilter>();
        for (int i = 0; i < allMF.Length; i++)
        {
            var mf = allMF[i];
            if (mf && mf.sharedMesh != null)
            {
                string n = mf.name.ToLower();
                string gn = mf.gameObject.name.ToLower();
                if (n.Contains("floor") || gn.Contains("floor"))
                {
                    floorMF = mf; break;
                }
            }
        }
        if (floorMF == null) return;

        Bounds b = floorMF.GetComponent<Renderer>() ? floorMF.GetComponent<Renderer>().bounds : new Bounds(floorMF.transform.TransformPoint(floorMF.sharedMesh.bounds.center),
                                                                                                          Vector3.Scale(floorMF.sharedMesh.bounds.size, floorMF.transform.lossyScale));
        Vector3 center = b.center;

        // Ajuste a piso
        Vector3 pos2 = center;
        RaycastHit hit2;
        if (Physics.Raycast(center + Vector3.up * 5f, Vector3.down, out hit2, 10f))
            pos2.y = hit2.point.y + eyeHeight;
        else
            pos2.y = floorY + eyeHeight;

        bool was2 = cc.enabled; cc.enabled = false;
        transform.position = pos2;
        cc.enabled = was2;
    }
}
