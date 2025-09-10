using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;
using UnityEngine.EventSystems;


public class ARPlacementController : MonoBehaviour
{
    public GameObject placementPrefab; // Prefab que vas a colocar
    private GameObject spawnedObject;

    private ARRaycastManager raycastManager;
    private List<ARRaycastHit> hits = new List<ARRaycastHit>();

    public TextMeshProUGUI debugText; // Arrastrar desde el editor

    // Evento personalizado para notificar cuando se coloca un objeto
    public event Action<Vector3> OnObjectPlaced;

    void Awake()
    {
        raycastManager = FindObjectOfType<ARRaycastManager>();
    }

    public void ColocarPuntoDesdeReticula()
    {
        Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);

        if (raycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            spawnedObject = Instantiate(placementPrefab, hitPose.position, hitPose.rotation);
            debugText.text = "Instancia colocada desde retícula";

            OnObjectPlaced?.Invoke(hitPose.position);
        }
        else
        {
            debugText.text = "No se detectó plano desde el centro";
        }
    }


    GameObject GetUIElementUnderTouch(Vector2 touchPosition)
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = touchPosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        return results.Count > 0 ? results[0].gameObject : null;
    }



    bool IsTouchOverUIObject(Vector2 touchPosition)
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = touchPosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }
}