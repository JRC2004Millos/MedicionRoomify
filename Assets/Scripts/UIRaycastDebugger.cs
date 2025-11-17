using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIRaycastDebugger : MonoBehaviour
{
    void Update()
    {
        #if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
        {
            LogRaycast(Input.mousePosition);
        }
        #else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            LogRaycast(Input.GetTouch(0).position);
        }
        #endif
    }

    void LogRaycast(Vector2 screenPos)
    {
        if (EventSystem.current == null)
        {
            Debug.LogWarning("[UIRaycastDebugger] No hay EventSystem");
            return;
        }

        var data = new PointerEventData(EventSystem.current) { position = screenPos };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);

        Debug.Log("=== UI Raycast ===");
        if (results.Count == 0)
        {
            Debug.Log("  (nada)");
            return;
        }

        foreach (var r in results)
        {
            Debug.Log($"  {r.gameObject.name}  (depth={r.depth})");
        }
    }
}
