using UnityEngine;

[DefaultExecutionOrder(200)]
public class PlayerRoomClamp : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RoomSpace room;      // arrastra tu RoomSpace
    [SerializeField] RoomBuilder builder; // arrastra tu RoomBuilder
    [SerializeField] CharacterController cc; // CharacterController en tu Main Camera

    [Header("Vertical control")]
    public bool enableFreeVertical = true; // <- activa “fly”
    public float verticalSpeed = 2.0f;     // m/s al subir/bajar
    public KeyCode upKey = KeyCode.Space;
    public KeyCode downKey = KeyCode.LeftControl;

    [Header("Comfort")]
    public float eyeHeightIfNoCC = 1.6f;   // si no hay CC
    public float epsilon = 0.002f;

    void Reset()
    {
        if (!room) room = FindFirstObjectByType<RoomSpace>();
        if (!builder) builder = FindFirstObjectByType<RoomBuilder>();
        if (!cc) cc = GetComponent<CharacterController>();
    }

    void LateUpdate()
    {
        if (!room) return;

        // --- límites verticales del cuarto ---
        float floorY = room.floorY; // 0.05 m en tu build de piso:contentReference[oaicite:2]{index=2}
        float roomH  = builder ? builder.RoomHeightMeters : 2.5f; // desde JSON:contentReference[oaicite:3]{index=3}

        // mitad de la “altura del jugador”
        float halfH = cc ? (cc.height * 0.5f) : (eyeHeightIfNoCC * 0.5f);
        float skin  = cc ? Mathf.Max(0.02f, cc.skinWidth) : 0.05f;

        float minY = floorY + halfH + skin + epsilon;
        float maxY = floorY + roomH - halfH - skin - epsilon;

        Vector3 pos = transform.position;

        // --- control vertical libre (fly) opcional ---
        if (enableFreeVertical)
        {
            float v = 0f;
            if (Input.GetKey(upKey))   v += 1f;
            if (Input.GetKey(downKey)) v -= 1f;
            pos.y += v * verticalSpeed * Time.deltaTime;
        }

        // clamp vertical siempre
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        // --- clamp XZ usando tu polígono/bounds ---
        Vector3 clampedXZ = room.ClampWorldToInside(pos); // fija Y a floorY; restaura la Y calculada
        clampedXZ.y = pos.y;

        Vector3 delta = clampedXZ - transform.position;

        if (delta.sqrMagnitude > 1e-8f)
        {
            if (cc) cc.Move(delta);    // suave si usas CharacterController
            else    transform.position += delta;
        }
    }
}
