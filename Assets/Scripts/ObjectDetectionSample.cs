using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Niantic.Lightship.AR.ObjectDetection;
using System.IO; // ✅ Necesario para JSON

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

    private Color[] _colors = new Color[]
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        Color.magenta,
        Color.cyan,
        Color.white,
        Color.black,
    };

    [SerializeField] private DrawRect _drawRect;
    private Canvas _canvas;
    private List<DetectedObjectData> _detectedObjectsList = new List<DetectedObjectData>();
    private string _jsonFilePath;

    private void Awake()
    {
        _canvas = FindObjectOfType<Canvas>();
        _jsonFilePath = Path.Combine(Application.persistentDataPath, "detected_objects.json");
        Debug.Log($"📁 JSON se guardará en: {_jsonFilePath}");
    }

    private void Start()
    {
        _objectDetectionManager.enabled = true;
        _objectDetectionManager.MetadataInitialized += ObjectDetectionManagerOnMetadataInitialized;
    }

    private void ObjectDetectionManagerOnMetadataInitialized(ARObjectDetectionModelEventArgs obj)
    {
        _objectDetectionManager.ObjectDetectionsUpdated += ObjectDetectionManagerOnObjectDetectionsUpdated;
    }

    private void OnDestroy()
    {
        _objectDetectionManager.MetadataInitialized -= ObjectDetectionManagerOnMetadataInitialized;
        _objectDetectionManager.ObjectDetectionsUpdated -= ObjectDetectionManagerOnObjectDetectionsUpdated;
        SaveDetectionsToJson(); // ✅ Guardar al cerrar
    }

    private void ObjectDetectionManagerOnObjectDetectionsUpdated(ARObjectDetectionsUpdatedEventArgs obj)
    {
        string resultString = "";
        float _confidence = 0;
        string _name = "";
        var results = obj.Results;

        if (results == null)
        {
            return;
        }

        _drawRect.ClearRects();
        _detectedObjectsList.Clear(); // ✅ Limpiar lista anterior

        for (int i = 0; i < results.Count; i++)
        {
            var detection = results[i];
            var categorizations = detection.GetConfidentCategorizations(_probabilityThreshold);

            if (categorizations.Count <= 0)
            {
                continue; // ✅ Cambié break por continue
            }

            categorizations.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
            var categoryToDisplay = categorizations[0];
            _confidence = categoryToDisplay.Confidence;
            _name = categoryToDisplay.CategoryName;

            int h = Mathf.FloorToInt(_canvas.GetComponent<RectTransform>().rect.height);
            int w = Mathf.FloorToInt(_canvas.GetComponent<RectTransform>().rect.width);

            var _rect = results[i].CalculateRect(w, h, Screen.orientation);

            resultString = $"{_name}: {_confidence:F2}\n";

            _drawRect.CreateRect(_rect, _colors[i % _colors.Length], resultString);

            // ✅ AGREGAR DATOS AL JSON
            DetectedObjectData newDetection = new DetectedObjectData
            {
                categoryName = _name,
                confidence = _confidence,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                position = new Vector2(_rect.x, _rect.y)
            };
            _detectedObjectsList.Add(newDetection);
        }

        // ✅ GUARDAR JSON después de procesar todas las detecciones
        SaveDetectionsToJson();
    }

    // ✅ MÉTODO PARA GUARDAR JSON
    private void SaveDetectionsToJson()
    {
        if (_detectedObjectsList.Count == 0)
            return;

        // Convertir a JSON
        string jsonData = JsonUtility.ToJson(new DetectionWrapper { detections = _detectedObjectsList }, true);
        
        // Guardar archivo
        File.WriteAllText(_jsonFilePath, jsonData);
        
        Debug.Log($"💾 JSON guardado: {_detectedObjectsList.Count} objetos en {_jsonFilePath}");
    }

    // ✅ Clase wrapper para la lista
    [System.Serializable]
    private class DetectionWrapper
    {
        public List<DetectedObjectData> detections;
    }

    // ✅ Método para leer el JSON desde otros scripts
    public List<DetectedObjectData> GetDetectedObjects()
    {
        if (File.Exists(_jsonFilePath))
        {
            string json = File.ReadAllText(_jsonFilePath);
            DetectionWrapper wrapper = JsonUtility.FromJson<DetectionWrapper>(json);
            return wrapper.detections;
        }
        return new List<DetectedObjectData>();
    }
}