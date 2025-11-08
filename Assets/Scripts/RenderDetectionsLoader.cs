/*
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class DetectionPosition { public float x; public float y; }

[Serializable]
public class DetectionEntry
{
    public string categoryName;
    public float confidence;
    public string timestamp;
    public DetectionPosition position;
    
    // NUEVO: Datos de cámara
    public Vector3 cameraPosition = Vector3.zero;
    public Quaternion cameraRotation = Quaternion.identity;
    public float cameraFOV = 60f;
}

[Serializable]
public class DetectionWrapper
{
    public List<DetectionEntry> detections = new List<DetectionEntry>();
    public int sourceScreenWidth = 0;
    public int sourceScreenHeight = 0;
    public bool coordsAreNormalized = true;
    public bool yOriginIsTop = true;
}

public class RenderDetectionsLoader : MonoBehaviour
{
    [Header("Archivo JSON de detecciones")]
    public string absoluteJsonPathOverride = "";
    public string fileName = "detected_objects.json";

    [Header("Escena destino")]
    public string targetSceneName = "RenderScene";

    [Header("Mapeo categoría → Prefab (en Resources/Prefabs)")]
    public List<CategoryPrefab> categoryPrefabs = new List<CategoryPrefab>()
    {
        new CategoryPrefab { categoryName = "screen",            prefabResourcePath = "Prefabs/Screen" },
        new CategoryPrefab { categoryName = "computer_mouse",    prefabResourcePath = "Prefabs/Computer_Mouse" },
        new CategoryPrefab { categoryName = "computer_keyboard", prefabResourcePath = "Prefabs/Computer_Keyboard" },
        new CategoryPrefab { categoryName = "furniture",         prefabResourcePath = "Prefabs/Furniture_Generic" },
        new CategoryPrefab { categoryName = "chair",             prefabResourcePath = "Prefabs/Chair" },
    };

    [Header("Posicionamiento")]
    public PlacementMode placementMode = PlacementMode.CameraRaycast; // ✅ NUEVO MODO POR DEFECTO
    public float fixedDepth = 2.0f;
    public float groundY = 0.0f;
    public float maxRaycastDistance = 10f; // ✅ NUEVO

    [Header("Integración con cuarto (RoomSpace)")]
    public RoomSpace roomSpace;
    public bool clampUVToRoom = true;
    public bool invertY = false;

    [Header("Overrides de normalización")]
    public bool forceCoordsNormalized = false;
    public bool forceYOriginTop = true;

    [Header("Escala manual (post-auto)")]
    public float globalScale = 0.1f;
    public float yOffset = 0.0f;

    [Serializable]
    public class CategoryScale { public string category; public float scale = 1f; }
    public List<CategoryScale> perCategoryScale = new List<CategoryScale>();

    [Header("Auto-escalado relativo al cuarto")]
    public bool autoScaleToRoom = true;
    [Range(0.01f, 1f)]
    public float defaultRoomWidthFraction = 0.15f;

    [Serializable]
    public class CategoryRoomFraction { public string category; [Range(0.01f, 1f)] public float fraction = 0.15f; }
    public List<CategoryRoomFraction> perCategoryRoomFraction = new List<CategoryRoomFraction>()
    {
        new CategoryRoomFraction{ category = "chair",             fraction = 0.18f },
        new CategoryRoomFraction{ category = "computer_keyboard", fraction = 0.25f },
        new CategoryRoomFraction{ category = "computer_mouse",    fraction = 0.10f },
        new CategoryRoomFraction{ category = "screen",            fraction = 0.35f },
        new CategoryRoomFraction{ category = "furniture",         fraction = 0.40f },
    };
    public float minFinalScale = 0.02f;
    public float maxFinalScale = 5f;

    [Header("Updates en vivo")]
    public float pollIntervalSeconds = 1.0f;
    public bool clearAndRebuildOnEveryChange = false;

    [Header("Debug / Seguridad")]
    public bool forceInsideRoom = true;
    [Range(0f, 0.45f)]
    public float uvMargin = 0.08f;
    public bool parentUnderRoomRoot = true;
    public bool logPositions = true;
    public bool drawHitGizmos = true;
    public bool drawCameraRays = true; // ✅ NUEVO

    // internos
    private string _path;
    private DateTime _lastWriteTime;
    private Camera _cam;
    private Dictionary<string, GameObject> _cachePrefabs = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> _spawnedByKey = new Dictionary<string, GameObject>();
    private DetectionWrapper _lastWrapperMeta;
    private List<Vector3> _lastPlantedWorldPoints = new List<Vector3>();
    private List<RayDebugInfo> _rayDebugInfos = new List<RayDebugInfo>(); // ✅ NUEVO

    [Serializable]
    public class CategoryPrefab
    {
        public string categoryName;
        public string prefabResourcePath;
    }

    public enum PlacementMode
    {
        FixedDepthFromCamera,
        GroundPlane,
        RoomSpaceAABB,
        CameraRaycast // ✅ NUEVO: Usa datos de cámara del JSON
    }

    // ✅ NUEVO: Para debug visual de raycast
    private struct RayDebugInfo
    {
        public Vector3 origin;
        public Vector3 direction;
        public float distance;
        public bool hit;
        public Vector3 hitPoint;
        public string category;
    }

    private void Awake()
    {
        if (!string.IsNullOrEmpty(absoluteJsonPathOverride))
        {
            _path = absoluteJsonPathOverride;
        }
        else
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _path = Path.Combine(Application.persistentDataPath, fileName);
#else
            string streaming = Path.Combine(Application.streamingAssetsPath, fileName);
            _path = File.Exists(streaming) ? streaming : Path.Combine(Application.persistentDataPath, fileName);
#endif
        }
        Debug.Log($"📄 Usando JSON: {_path}");

        _cam = Camera.main;
        if (_cam == null) Debug.LogWarning("⚠️ Camera.main es null. Asigna una cámara con la tag 'MainCamera'.");

        if (roomSpace == null) roomSpace = FindFirstObjectByType<RoomSpace>();
    }

    private async void Start()
    {
        if (SceneManager.GetActiveScene().name != targetSceneName)
            Debug.LogWarning($"⚠️ Estás en '{SceneManager.GetActiveScene().name}'. Se esperaba '{targetSceneName}'.");

        await TryLoadAndSpawn(forceRebuild: true);
        InvokeRepeating(nameof(CheckForUpdates), pollIntervalSeconds, pollIntervalSeconds);
    }

    private void CheckForUpdates()
    {
        try
        {
            if (File.Exists(_path))
            {
                var t = File.GetLastWriteTimeUtc(_path);
                if (t != _lastWriteTime)
                {
                    _lastWriteTime = t;
                    _ = TryLoadAndSpawn(forceRebuild: clearAndRebuildOnEveryChange);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error revisando cambios: {e.Message}");
        }
    }

    private async Task TryLoadAndSpawn(bool forceRebuild)
    {
        DetectionWrapper wrapper = null;

        try
        {
            wrapper = await LoadDetectionsAsync(_path);
            _lastWrapperMeta = wrapper;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error leyendo JSON: {e.Message}");
            return;
        }

        if (wrapper == null || wrapper.detections == null) return;

        if (forceRebuild)
        {
            foreach (var go in _spawnedByKey.Values) if (go) Destroy(go);
            _spawnedByKey.Clear();
        }

        _lastPlantedWorldPoints.Clear();
        _rayDebugInfos.Clear(); // ✅ NUEVO

        int added = 0;
        foreach (var det in wrapper.detections)
        {
            if (det.position == null) continue;
            var key = $"{det.categoryName}|{det.timestamp}|{det.position.x:F3}|{det.position.y:F3}";
            if (_spawnedByKey.ContainsKey(key) && _spawnedByKey[key] != null) continue;

            var prefab = ResolvePrefab(det.categoryName);
            if (prefab == null) continue;

            // 1) Normalización UV
            Vector2 uv = NormalizeXY(det.position.x, det.position.y, _lastWrapperMeta);

            // 2) ✅ NUEVO: UV -> posición mundo usando datos de cámara del JSON
            Vector3 worldPos = UVToWorld(uv, det);

            // 3) Parent adecuado
            Transform parent = (parentUnderRoomRoot && roomSpace != null && roomSpace.roomRoot != null)
                ? roomSpace.roomRoot : null;

            // 4) Instanciar
            var go = Instantiate(prefab, worldPos, Quaternion.identity, parent);
            go.name = $"{det.categoryName}_{det.timestamp}".Replace(" ", "_").Replace(":", "-");

            // 5) Alinear base del prefab al piso
            AlignPrefabBaseToFloor(go);

            // 6) Auto-escala + manual
            float finalScale = 1f;
            if (autoScaleToRoom && roomSpace != null && roomSpace.HasValidBounds())
                finalScale *= ComputeAutoScaleAgainstRoom(prefab, det.categoryName);
            finalScale *= ResolveManualScaleFor(det.categoryName);
            finalScale = Mathf.Clamp(finalScale, minFinalScale, maxFinalScale);
            go.transform.localScale = go.transform.localScale * finalScale;

            // 7) Clamp definitivo (AABB o polígono)
            if (roomSpace != null) 
                go.transform.position = roomSpace.ClampWorldToInside(go.transform.position + new Vector3(0f, yOffset, 0f));

            _spawnedByKey[key] = go;
            added++;
            _lastPlantedWorldPoints.Add(go.transform.position);
        }

        if (added > 0)
            Debug.Log($"🧩 Instanciados nuevos: {added} (total en escena: {_spawnedByKey.Count})");
    }

    private async Task<DetectionWrapper> LoadDetectionsAsync(string fullPath)
    {
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"No existe el archivo: {fullPath}");
        string json = File.ReadAllText(fullPath);
        await Task.Yield();

        var wrapper = JsonUtility.FromJson<DetectionWrapper>(json);
        _lastWriteTime = File.GetLastWriteTimeUtc(fullPath);
        return wrapper;
    }

    private GameObject ResolvePrefab(string category)
    {
        if (string.IsNullOrEmpty(category)) return null;
        if (_cachePrefabs.TryGetValue(category, out var pf)) return pf;

        foreach (var map in categoryPrefabs)
        {
            if (string.Equals(map.categoryName, category, StringComparison.OrdinalIgnoreCase))
            {
                var loaded = Resources.Load<GameObject>(map.prefabResourcePath);
                if (loaded == null)
                {
                    Debug.LogError($"❌ Prefab no encontrado: Resources/{map.prefabResourcePath}.prefab (categoría '{category}')");
                    _cachePrefabs[category] = null;
                    return null;
                }
                _cachePrefabs[category] = loaded;
                return loaded;
            }
        }

        Debug.LogWarning($"⚠️ Sin mapeo para categoría '{category}'.");
        _cachePrefabs[category] = null;
        return null;
    }

    // =================== NORMALIZACIÓN & POSICIONAMIENTO ===================

    private Vector2 NormalizeXY(float px, float py, DetectionWrapper meta)
    {
        float u, v;

        bool metaHasWH = (meta != null && meta.sourceScreenWidth > 0 && meta.sourceScreenHeight > 0);
        bool looksLikePixels = metaHasWH && (px > 1.0f || py > 1.0f);

        if (!forceCoordsNormalized && looksLikePixels)
        {
            u = px / meta.sourceScreenWidth;
            v = py / meta.sourceScreenHeight;
        }
        else if (!forceCoordsNormalized && meta != null && meta.coordsAreNormalized)
        {
            u = px; v = py;
        }
        else if (forceCoordsNormalized)
        {
            u = px; v = py;
        }
        else
        {
            u = Mathf.InverseLerp(0f, Screen.width, px);
            v = Mathf.InverseLerp(0f, Screen.height, py);
        }

        bool yTop = forceYOriginTop || (meta != null && meta.yOriginIsTop);
        if (yTop) v = 1f - v;

        if (clampUVToRoom)
        {
            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);
        }
        if (forceInsideRoom)
        {
            float m = Mathf.Clamp01(uvMargin);
            u = Mathf.Lerp(m, 1f - m, u);
            v = Mathf.Lerp(m, 1f - m, v);
        }
        if (invertY) v = 1f - v;

        return new Vector2(u, v);
    }

    // ✅ ACTUALIZADO: Ahora recibe DetectionEntry completo
    private Vector3 UVToWorld(Vector2 uv, DetectionEntry detection)
    {
        switch (placementMode)
        {
            case PlacementMode.CameraRaycast:
                return UVToWorld_CameraRaycast(uv, detection);

            case PlacementMode.RoomSpaceAABB:
                if (roomSpace != null && roomSpace.HasValidBounds())
                {
                    float x = Mathf.Lerp(roomSpace.minX, roomSpace.maxX, uv.x);
                    float z = Mathf.Lerp(roomSpace.minZ, roomSpace.maxZ, uv.y);
                    float y = (roomSpace != null ? roomSpace.floorY : 0f);
                    var world = new Vector3(x, y, z);
                    if (logPositions)
                        Debug.Log($"[Detections] UV={uv:F3} → World={world:F3} | Room[minX={roomSpace.minX:F2}, maxX={roomSpace.maxX:F2}, minZ={roomSpace.minZ:F2}, maxZ={roomSpace.maxZ:F2}]");
                    return world;
                }
                Debug.LogWarning("⚠️ RoomSpace no asignado o sin bounds válidos. Fallback a FixedDepth.");
                return UVToWorld_FixedDepth(uv);

            case PlacementMode.GroundPlane:
                if (_cam == null) _cam = Camera.main;
                if (_cam == null) return new Vector3(0f, yOffset, fixedDepth);
                float px = uv.x * Screen.width;
                float py = uv.y * Screen.height;
                Ray ray = _cam.ScreenPointToRay(new Vector3(px, py, 0f));
                Plane plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
                if (plane.Raycast(ray, out float enter))
                {
                    Vector3 pos = ray.GetPoint(enter);
                    return pos;
                }
                return UVToWorld_FixedDepth(uv);

            case PlacementMode.FixedDepthFromCamera:
            default:
                return UVToWorld_FixedDepth(uv);
        }
    }

    // ✅ NUEVO: Proyección real usando datos de cámara del JSON
    private Vector3 UVToWorld_CameraRaycast(Vector2 uv, DetectionEntry detection)
    {
        // Verificar que tenemos datos de cámara válidos
        bool hasCameraData = detection.cameraPosition != Vector3.zero || 
                             detection.cameraRotation != Quaternion.identity;
        
        if (!hasCameraData)
        {
            Debug.LogWarning($"⚠️ Detección sin datos de cámara, usando fallback AABB para {detection.categoryName}");
            return UVToWorld_RoomSpaceAABB_Fallback(uv);
        }

        // Crear matriz de proyección virtual usando FOV del JSON
        float aspect = _lastWrapperMeta != null && _lastWrapperMeta.sourceScreenWidth > 0 
            ? (float)_lastWrapperMeta.sourceScreenWidth / _lastWrapperMeta.sourceScreenHeight 
            : 1f;
        
        Matrix4x4 projectionMatrix = Matrix4x4.Perspective(detection.cameraFOV, aspect, 0.1f, 100f);
        Matrix4x4 worldToCameraMatrix = Matrix4x4.TRS(detection.cameraPosition, detection.cameraRotation, Vector3.one).inverse;
        
        // Convertir UV a NDC (Normalized Device Coordinates)
        float ndcX = uv.x * 2f - 1f; // [-1, 1]
        float ndcY = uv.y * 2f - 1f; // [-1, 1]
        
        // Crear puntos en near y far plane
        Vector4 nearPoint = new Vector4(ndcX, ndcY, -1f, 1f); // near plane
        Vector4 farPoint = new Vector4(ndcX, ndcY, 1f, 1f);   // far plane
        
        // Invertir proyección
        Matrix4x4 inverseProjection = projectionMatrix.inverse;
        Vector4 nearView = inverseProjection * nearPoint;
        Vector4 farView = inverseProjection * farPoint;
        
        nearView /= nearView.w;
        farView /= farView.w;
        
        // Convertir a espacio mundo
        Matrix4x4 cameraToWorld = worldToCameraMatrix.inverse;
        Vector3 nearWorld = cameraToWorld.MultiplyPoint3x4(nearView);
        Vector3 farWorld = cameraToWorld.MultiplyPoint3x4(farView);
        
        // Crear ray desde la cámara
        Vector3 rayOrigin = detection.cameraPosition;
        Vector3 rayDirection = (farWorld - nearWorld).normalized;
        
        // Raycast hacia el piso del cuarto
        float floorY = roomSpace != null ? roomSpace.floorY : 0f;
        Plane floorPlane = new Plane(Vector3.up, new Vector3(0f, floorY, 0f));
        
        Ray castRay = new Ray(rayOrigin, rayDirection);
        Vector3 hitPoint;
        bool didHit = false;
        
        if (floorPlane.Raycast(castRay, out float distance))
        {
            if (distance > 0 && distance < maxRaycastDistance)
            {
                hitPoint = castRay.GetPoint(distance);
                didHit = true;
                
                // Verificar que está dentro del cuarto
                if (roomSpace != null && !roomSpace.ContainsXZ(hitPoint))
                {
                    // Si está fuera, clampearlo al borde más cercano
                    hitPoint = roomSpace.ClampWorldToInside(hitPoint);
                    if (logPositions)
                        Debug.Log($"📍 Objeto fuera del cuarto, clampeado: {detection.categoryName}");
                }
            }
            else
            {
                // Distancia fuera de rango
                hitPoint = UVToWorld_RoomSpaceAABB_Fallback(uv);
                if (logPositions)
                    Debug.LogWarning($"⚠️ Raycast distancia fuera de rango ({distance:F2}m), usando fallback para {detection.categoryName}");
            }
        }
        else
        {
            // No hit con el piso, usar fallback
            hitPoint = UVToWorld_RoomSpaceAABB_Fallback(uv);
            if (logPositions)
                Debug.LogWarning($"⚠️ Raycast sin hit con piso, usando fallback para {detection.categoryName}");
        }
        
        // Guardar info de debug
        _rayDebugInfos.Add(new RayDebugInfo
        {
            origin = rayOrigin,
            direction = rayDirection,
            distance = distance,
            hit = didHit,
            hitPoint = hitPoint,
            category = detection.categoryName
        });
        
        if (logPositions)
        {
            Debug.Log($"🎯 CameraRaycast: {detection.categoryName}\n" +
                     $"   UV: {uv:F3}\n" +
                     $"   CamPos: {rayOrigin:F3}\n" +
                     $"   CamRot: {detection.cameraRotation.eulerAngles:F1}\n" +
                     $"   Ray: {rayDirection:F3}\n" +
                     $"   Hit: {hitPoint:F3} (dist: {distance:F2}m)");
        }
        
        return hitPoint;
    }

    // ✅ NUEVO: Fallback si no hay datos de cámara
    private Vector3 UVToWorld_RoomSpaceAABB_Fallback(Vector2 uv)
    {
        if (roomSpace != null && roomSpace.HasValidBounds())
        {
            float x = Mathf.Lerp(roomSpace.minX, roomSpace.maxX, uv.x);
            float z = Mathf.Lerp(roomSpace.minZ, roomSpace.maxZ, uv.y);
            float y = roomSpace.floorY;
            return new Vector3(x, y, z);
        }
        return new Vector3(0f, 0f, 0f);
    }

    private Vector3 UVToWorld_FixedDepth(Vector2 uv)
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return new Vector3(0f, yOffset, fixedDepth);
        float px = uv.x * Screen.width;
        float py = uv.y * Screen.height;
        Vector3 screen = new Vector3(px, py, fixedDepth);
        Vector3 pos = _cam.ScreenToWorldPoint(screen);
        return pos;
    }

    // ======= ESCALA & AJUSTES DE PREFAB =======

    private float ResolveManualScaleFor(string category)
    {
        float scale = globalScale <= 0f ? 1f : globalScale;
        if (!string.IsNullOrEmpty(category) && perCategoryScale != null)
        {
            foreach (var cs in perCategoryScale)
            {
                if (cs != null && string.Equals(cs.category, category, StringComparison.OrdinalIgnoreCase))
                {
                    scale *= (cs.scale <= 0f ? 1f : cs.scale);
                    break;
                }
            }
        }
        return scale;
    }

    private float ComputeAutoScaleAgainstRoom(GameObject prefab, string category)
    {
        if (prefab == null || roomSpace == null || !roomSpace.HasValidBounds())
            return 1f;

        float roomWidth = Mathf.Abs(roomSpace.maxX - roomSpace.minX);
        if (roomWidth <= 0.0001f) return 1f;

        float frac = defaultRoomWidthFraction;
        if (perCategoryRoomFraction != null)
        {
            foreach (var cf in perCategoryRoomFraction)
            {
                if (cf != null && string.Equals(cf.category, category, StringComparison.OrdinalIgnoreCase))
                {
                    frac = Mathf.Clamp(cf.fraction, 0.01f, 1f);
                    break;
                }
            }
        }

        float targetMeters = roomWidth * frac;

        Vector3 size = GetPrefabBoundsSize(prefab);
        float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        if (longest <= 0.0001f) return 1f;

        return targetMeters / longest;
    }

    private static Vector3 GetPrefabBoundsSize(GameObject prefab)
    {
        if (prefab == null) return Vector3.one;
        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return Vector3.one;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b.size;
    }

    private void AlignPrefabBaseToFloor(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends == null || rends.Length == 0) return;

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        float currentBaseY = b.min.y;
        float targetBaseY = (roomSpace != null ? roomSpace.floorY : 0f) + yOffset;

        float delta = targetBaseY - currentBaseY;
        go.transform.position += new Vector3(0f, delta, 0f);
    }

    // ✅ ACTUALIZADO: Gizmos con raycast debug
    private void OnDrawGizmos()
    {
        if (!drawHitGizmos) return;
        
        // Dibujar objetos spawneados
        Gizmos.color = Color.magenta;
        foreach (var p in _lastPlantedWorldPoints)
        {
            Gizmos.DrawSphere(p + Vector3.up * 0.02f, 0.04f);
        }
        
        // ✅ NUEVO: Dibujar raycast debug
        if (drawCameraRays && _rayDebugInfos.Count > 0)
        {
            foreach (var rayInfo in _rayDebugInfos)
            {
                if (rayInfo.hit)
                {
                    // Ray exitoso: verde
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(rayInfo.origin, rayInfo.hitPoint);
                    Gizmos.DrawWireSphere(rayInfo.hitPoint, 0.05f);
                }
                else
                {
                    // Ray fallido: rojo
                    Gizmos.color = Color.red;
                    Vector3 endPoint = rayInfo.origin + rayInfo.direction * maxRaycastDistance;
                    Gizmos.DrawLine(rayInfo.origin, endPoint);
                }
                
                // Dibujar origen de cámara
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(rayInfo.origin, 0.08f);
            }
        }
    }

    public void SetJsonPath(string p)
    {
        if (string.IsNullOrEmpty(p)) return;
        absoluteJsonPathOverride = p;
        _path = p;

        CancelInvoke(nameof(CheckForUpdates));
        _ = TryLoadAndSpawn(forceRebuild: true);
        InvokeRepeating(nameof(CheckForUpdates), pollIntervalSeconds, pollIntervalSeconds);

        Debug.Log($"🔄 Ruta de JSON actualizada: {p}");
    }
}*/