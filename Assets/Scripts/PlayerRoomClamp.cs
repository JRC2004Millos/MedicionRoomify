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
        float floorY = room.floorY; // viene de RoomSpace (piso), ya lo tienes aquí
        float roomH = builder ? builder.RoomHeightMeters : 2.5f; // altura confiable

        // mitad de la “altura del jugador” (funciona con o sin CharacterController)
        float halfH = cc ? (cc.height * 0.5f) : (eyeHeightIfNoCC * 0.5f);
        float skin = cc ? Mathf.Max(0.02f, cc.skinWidth) : 0.05f;

        float minY = floorY + halfH + skin + epsilon;
        float maxY = floorY + roomH - halfH - skin - epsilon;

        Vector3 pos = transform.position;

        // --- control vertical libre (fly) opcional ---
        if (enableFreeVertical)
        {
            float v = 0f;
            if (Input.GetKey(upKey)) v += 1f;
            if (Input.GetKey(downKey)) v -= 1f;
            if (Mathf.Abs(v) > 0f)
            {
                float dy = v * verticalSpeed * Time.deltaTime;
                if (cc) cc.Move(Vector3.up * dy);
                else pos.y += dy;
            }
        }

        // Clamp final de Y
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        // --- clamp XZ con tu RoomSpace ---
        Vector3 clampedXZ = room.ClampWorldToInside(pos); // este método tiende a fijar Y = floorY
        clampedXZ.y = pos.y; // recupera la Y recién clampeada

        Vector3 delta = clampedXZ - transform.position;
        if (delta.sqrMagnitude > 1e-8f)
        {
            if (cc) cc.Move(delta);
            else transform.position += delta;
        }

    }
}
