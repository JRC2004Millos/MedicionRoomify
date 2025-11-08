using UnityEngine;

/// <summary>
/// Ajusta el RectTransform del objeto a la zona segura del dispositivo (evita notch o botones).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform panel;
    Rect lastSafeArea = new Rect(0, 0, 0, 0);
    Vector2Int lastScreenSize = new Vector2Int(0, 0);

    void Awake()
    {
        panel = GetComponent<RectTransform>();
        ApplySafeArea(Screen.safeArea);
    }

    void Update()
    {
        // Detecta cambio de orientación o tamaño
        if (Screen.safeArea != lastSafeArea || 
            Screen.width != lastScreenSize.x || 
            Screen.height != lastScreenSize.y)
        {
            ApplySafeArea(Screen.safeArea);
        }
    }

    void ApplySafeArea(Rect r)
    {
        if (panel == null) return;

        lastSafeArea = r;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        // Normaliza valores (0–1)
        Vector2 anchorMin = r.position;
        Vector2 anchorMax = r.position + r.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        panel.anchorMin = anchorMin;
        panel.anchorMax = anchorMax;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
    }
}
