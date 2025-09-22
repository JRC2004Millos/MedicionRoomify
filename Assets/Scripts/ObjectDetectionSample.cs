using UnityEngine;
using System.Collections.Generic;
using System;
using Niantic.Lightship.AR.ObjectDetection;
using System.IO;

[System.Serializable]
public class DetectedObjectData
{
    public string categoryName;
    public float confidence;
    public string timestamp;
    public Vector2 position;
}

public class ObjectDetectionSample : MonoBehaviour
{
    [SerializeField] private float _probabilityThreshold = .5f;
    [SerializeField] private ARObjectDetectionManager _objectDetectionManager;

    private List<DetectedObjectData> _detectedObjectsList = new List<DetectedObjectData>();
    private string _jsonFilePath;

    private void Awake()
    {
        _jsonFilePath = Path.Combine(Application.persistentDataPath, "detected_objects.json");
        Debug.Log($"📁 JSON se guardará en: {_jsonFilePath}");
    }

    private void Start()
    {
        if (_objectDetectionManager == null)
        {
            Debug.LogError("❌ ARObjectDetectionManager no está asignado");
            return;
        }
        
        _objectDetectionManager.enabled = true;
        _objectDetectionManager.MetadataInitialized += ObjectDetectionManagerOnMetadataInitialized;
    }

    private void ObjectDetectionManagerOnMetadataInitialized(ARObjectDetectionModelEventArgs obj)
    {
        if (_objectDetectionManager != null)
        {
            _objectDetectionManager.ObjectDetectionsUpdated += ObjectDetectionManagerOnObjectDetectionsUpdated;
        }
    }

    private void OnDestroy()
    {
        if (_objectDetectionManager != null)
        {
            _objectDetectionManager.MetadataInitialized -= ObjectDetectionManagerOnMetadataInitialized;
            _objectDetectionManager.ObjectDetectionsUpdated -= ObjectDetectionManagerOnObjectDetectionsUpdated;
        }
        SaveDetectionsToJson();
    }

    private void ObjectDetectionManagerOnObjectDetectionsUpdated(ARObjectDetectionsUpdatedEventArgs obj)
    {
        var results = obj.Results;

        // ✅ VERIFICAR RESULTADOS
        if (results == null)
        {
            Debug.Log("📋 No hay resultados de detección");
            return;
        }

        _detectedObjectsList.Clear();

        for (int i = 0; i < results.Count; i++)
        {
            var detection = results[i];
            
            // ✅ VERIFICAR DETECCIÓN INDIVIDUAL
            if (detection == null)
            {
                Debug.LogWarning($"⚠️ Detección {i} es null");
                continue;
            }

            var categorizations = detection.GetConfidentCategorizations(_probabilityThreshold);

            if (categorizations == null || categorizations.Count <= 0)
            {
                continue;
            }

            categorizations.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
            var categoryToDisplay = categorizations[0];

            float confidence = categoryToDisplay.Confidence;
            string categoryName = categoryToDisplay.CategoryName ?? "Unknown";

            // ✅ OBTENER POSICIÓN (usando center del boundingBox si está disponible)
            Vector2 position = Vector2.zero;
            try
            {
                // Intentar obtener la posición del centro del objeto detectado
                var boundingBox = detection.CalculateRect(Screen.width, Screen.height, Screen.orientation);
                position = new Vector2(boundingBox.center.x, boundingBox.center.y);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"⚠️ No se pudo calcular posición: {e.Message}");
            }

            // ✅ AGREGAR DATOS AL JSON
            DetectedObjectData newDetection = new DetectedObjectData
            {
                categoryName = categoryName,
                confidence = confidence,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                position = position
            };
            _detectedObjectsList.Add(newDetection);

            Debug.Log($"🎯 Detectado: {categoryName} ({confidence:F2}) en posición {position}");
        }

        // ✅ GUARDAR JSON después de procesar todas las detecciones
        SaveDetectionsToJson();
    }

    // ✅ MÉTODO PARA GUARDAR JSON CON MANEJO DE ERRORES
    private void SaveDetectionsToJson()
    {
        if (_detectedObjectsList == null || _detectedObjectsList.Count == 0)
            return;

        try
        {
            // Convertir a JSON
            string jsonData = JsonUtility.ToJson(new DetectionWrapper { detections = _detectedObjectsList }, true);
            
            // Guardar archivo
            File.WriteAllText(_jsonFilePath, jsonData);
            
            Debug.Log($"💾 JSON guardado: {_detectedObjectsList.Count} objetos en {_jsonFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error guardando JSON: {e.Message}");
        }
    }

    // ✅ Clase wrapper para la lista
    [System.Serializable]
    private class DetectionWrapper
    {
        public List<DetectedObjectData> detections;
    }

    // ✅ Método para leer el JSON desde otros scripts CON MANEJO DE ERRORES
    public List<DetectedObjectData> GetDetectedObjects()
    {
        try
        {
            if (File.Exists(_jsonFilePath))
            {
                string json = File.ReadAllText(_jsonFilePath);
                if (!string.IsNullOrEmpty(json))
                {
                    DetectionWrapper wrapper = JsonUtility.FromJson<DetectionWrapper>(json);
                    return wrapper?.detections ?? new List<DetectedObjectData>();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error leyendo JSON: {e.Message}");
        }
        
        return new List<DetectedObjectData>();
    }
}