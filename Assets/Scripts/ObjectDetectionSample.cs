using UnityEngine;
using System.Collections.Generic;
using System;
using Niantic.Lightship.AR.ObjectDetection;
using System.IO;
using System.Collections;

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
    
    [SerializeField] private float _autoSaveInterval = 5f;
    [SerializeField] private int _maxDetections = 1000;
    private List<DetectedObjectData> _detectedObjectsList = new List<DetectedObjectData>();
    private string _jsonFilePath;
    private Coroutine _autoSaveCoroutine;
    private bool _hasUnsavedData = false;

    private void Awake()
    {
        _jsonFilePath = Path.Combine(Application.persistentDataPath, "detected_objects.json");
        Debug.Log($"📁 JSON se guardará en: {_jsonFilePath}");
        
        LoadExistingDetections();
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
        
        _autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
    }

    private void ObjectDetectionManagerOnMetadataInitialized(ARObjectDetectionModelEventArgs obj)
    {
        if (_objectDetectionManager != null)
        {
            _objectDetectionManager.ObjectDetectionsUpdated += ObjectDetectionManagerOnObjectDetectionsUpdated;
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Debug.Log("📱 App pausada - Guardando JSON...");
            SaveDetectionsToJson();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            Debug.Log("📱 App perdió foco - Guardando JSON...");
            SaveDetectionsToJson();
        }
    }

    private void OnDestroy()
    {
        if (_objectDetectionManager != null)
        {
            _objectDetectionManager.MetadataInitialized -= ObjectDetectionManagerOnMetadataInitialized;
            _objectDetectionManager.ObjectDetectionsUpdated -= ObjectDetectionManagerOnObjectDetectionsUpdated;
        }
        
        if (_autoSaveCoroutine != null)
        {
            StopCoroutine(_autoSaveCoroutine);
        }
        
        SaveDetectionsToJson();
    }

    private void ObjectDetectionManagerOnObjectDetectionsUpdated(ARObjectDetectionsUpdatedEventArgs obj)
    {
        var results = obj.Results;

        if (results == null || results.Count == 0)
        {
            return;
        }

        List<DetectedObjectData> newDetections = new List<DetectedObjectData>();

        for (int i = 0; i < results.Count; i++)
        {
            var detection = results[i];
            
            if (detection == null)
            {
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

            Vector2 position = Vector2.zero;
            try
            {
                var boundingBox = detection.CalculateRect(Screen.width, Screen.height, Screen.orientation);
                position = new Vector2(boundingBox.center.x, boundingBox.center.y);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"⚠️ No se pudo calcular posición: {e.Message}");
            }

            DetectedObjectData newDetection = new DetectedObjectData
            {
                categoryName = categoryName,
                confidence = confidence,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                position = position
            };

            newDetections.Add(newDetection);
        }

        if (newDetections.Count > 0)
        {
            _detectedObjectsList.AddRange(newDetections);
            _hasUnsavedData = true;

            if (_detectedObjectsList.Count > _maxDetections)
            {
                int excess = _detectedObjectsList.Count - _maxDetections;
                _detectedObjectsList.RemoveRange(0, excess);
                Debug.Log($"🗑️ Eliminadas {excess} detecciones antiguas para mantener límite");
            }

            Debug.Log($"🎯 {newDetections.Count} nuevas detecciones agregadas. Total: {_detectedObjectsList.Count}");
        }
    }

    private IEnumerator AutoSaveCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_autoSaveInterval);
            
            if (_hasUnsavedData)
            {
                SaveDetectionsToJson();
            }
        }
    }

    private void LoadExistingDetections()
    {
        try
        {
            if (File.Exists(_jsonFilePath))
            {
                string json = File.ReadAllText(_jsonFilePath);
                if (!string.IsNullOrEmpty(json))
                {
                    DetectionWrapper wrapper = JsonUtility.FromJson<DetectionWrapper>(json);
                    if (wrapper?.detections != null)
                    {
                        _detectedObjectsList = wrapper.detections;
                        Debug.Log($"📂 Cargadas {_detectedObjectsList.Count} detecciones existentes");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error cargando detecciones existentes: {e.Message}");
            _detectedObjectsList = new List<DetectedObjectData>();
        }
    }

    private void SaveDetectionsToJson()
    {
        if (_detectedObjectsList == null || _detectedObjectsList.Count == 0)
        {
            Debug.Log("📝 No hay detecciones para guardar");
            return;
        }

        try
        {
            string directory = Path.GetDirectoryName(_jsonFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Debug.Log($"📁 Directorio creado: {directory}");
            }

            DetectionWrapper wrapper = new DetectionWrapper { detections = _detectedObjectsList };
            string jsonData = JsonUtility.ToJson(wrapper, true);
            
            File.WriteAllText(_jsonFilePath, jsonData);
            _hasUnsavedData = false;
            
            Debug.Log($"💾 JSON guardado exitosamente: {_detectedObjectsList.Count} detecciones en {_jsonFilePath}");
            
            if (File.Exists(_jsonFilePath))
            {
                FileInfo fileInfo = new FileInfo(_jsonFilePath);
                Debug.Log($"✅ Archivo confirmado: {fileInfo.Length} bytes");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error guardando JSON: {e.Message}");
            Debug.LogError($"❌ Stack trace: {e.StackTrace}");
        }
    }

    [ContextMenu("Guardar JSON Manualmente")]
    public void ManualSave()
    {
        SaveDetectionsToJson();
    }

    [ContextMenu("Limpiar Detecciones")]
    public void ClearDetections()
    {
        _detectedObjectsList.Clear();
        _hasUnsavedData = true;
        SaveDetectionsToJson();
        Debug.Log("🗑️ Todas las detecciones han sido eliminadas");
    }

    [System.Serializable]
    private class DetectionWrapper
    {
        public List<DetectedObjectData> detections = new List<DetectedObjectData>();
    }

    public List<DetectedObjectData> GetDetectedObjects()
    {
        return new List<DetectedObjectData>(_detectedObjectsList);
    }

    [ContextMenu("Mostrar Información")]
    public void ShowDebugInfo()
    {
        Debug.Log($"📊 INFORMACIÓN DE DEBUG:");
        Debug.Log($"📁 Ruta del archivo: {_jsonFilePath}");
        Debug.Log($"📝 Detecciones en memoria: {_detectedObjectsList.Count}");
        Debug.Log($"💾 Archivo existe: {File.Exists(_jsonFilePath)}");
        Debug.Log($"🔄 Datos sin guardar: {_hasUnsavedData}");
        
        if (File.Exists(_jsonFilePath))
        {
            try
            {
                FileInfo info = new FileInfo(_jsonFilePath);
                Debug.Log($"📄 Tamaño del archivo: {info.Length} bytes");
                Debug.Log($"📅 Última modificación: {info.LastWriteTime}");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Error obteniendo info del archivo: {e.Message}");
            }
        }
    }
}