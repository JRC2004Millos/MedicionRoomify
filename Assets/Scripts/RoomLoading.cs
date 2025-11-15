using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Carga el estado de una habitación desde un JSON generado por RoomSaving:
/// - Aplica texturas a paredes/piso/techo.
/// - Reconstruye los muebles en la escena.
/// Debe existir un archivo JSON compatible con RoomSaveData.
/// </summary>
public class RoomLoading : MonoBehaviour
{
    [Header("Ruta de layout (opcional)")]
    [Tooltip("Si se deja vacío en Android, se usa el Intent extra 'ROOM_LAYOUT_PATH'.")]
    public string layoutOverridePath = "";

    [Header("Superficies presentes en la escena")]
    [Tooltip("Paredes, piso y techo que deben recibir las texturas cargadas.")]
    public List<SurfaceBinding> sceneSurfaces = new List<SurfaceBinding>();

    [Header("Prefabs de muebles disponibles")]
    [Tooltip("Mapea prefabName (del JSON) con el prefab real en Unity.")]
    public List<FurniturePrefabEntry> furniturePrefabs = new List<FurniturePrefabEntry>();

    private Dictionary<string, SurfaceBinding> _surfaceMap;
    private Dictionary<string, GameObject> _furnitureMap;

    private void Awake()
    {
        BuildSurfaceMap();
        BuildFurnitureMap();
    }

    private void Start()
    {
        string path = ResolveLayoutPath();

        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[RoomLoading] No se proporcionó ruta de layout.");
            return;
        }

        if (!File.Exists(path))
        {
            Debug.LogError("[RoomLoading] El archivo de layout no existe: " + path);
            return;
        }

        Debug.Log("[RoomLoading] Cargando layout desde: " + path);

        string json = File.ReadAllText(path);
        RoomSaveData data = null;
        try
        {
            data = JsonUtility.FromJson<RoomSaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("[RoomLoading] Error al parsear JSON: " + e.Message);
            return;
        }

        if (data == null)
        {
            Debug.LogError("[RoomLoading] RoomSaveData nulo al deserializar JSON.");
            return;
        }

        Debug.Log($"[RoomLoading] Layout cargado. roomId={data.roomId}, spaceName={data.spaceName}");

        ApplyTextures(data);
        RebuildFurniture(data);
    }

    /// <summary>
    /// Resuelve la ruta del JSON de layout.
    /// </summary>
    private string ResolveLayoutPath()
    {
        if (!string.IsNullOrEmpty(layoutOverridePath))
        {
            return layoutOverridePath;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var intent   = activity.Call<AndroidJavaObject>("getIntent");
                string extra = intent.Call<string>("getStringExtra", "ROOM_LAYOUT_PATH");

                if (!string.IsNullOrEmpty(extra))
                {
                    Debug.Log("[RoomLoading] ROOM_LAYOUT_PATH desde Intent: " + extra);
                    return extra;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[RoomLoading] Error leyendo ROOM_LAYOUT_PATH del Intent: " + e.Message);
        }

        return null;
#else
        // Para pruebas en el editor, puedes poner aquí una ruta absoluta
        // o arrastrarla en layoutOverridePath desde el Inspector.
        return layoutOverridePath;
#endif
    }

    #region Maps auxiliares

    private void BuildSurfaceMap()
    {
        _surfaceMap = new Dictionary<string, SurfaceBinding>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sceneSurfaces)
        {
            if (s == null || string.IsNullOrEmpty(s.surfaceId) || s.renderer == null)
                continue;

            if (!_surfaceMap.ContainsKey(s.surfaceId))
            {
                _surfaceMap.Add(s.surfaceId, s);
            }
            else
            {
                Debug.LogWarning("[RoomLoading] surfaceId duplicado en sceneSurfaces: " + s.surfaceId);
            }
        }
    }

    private void BuildFurnitureMap()
    {
        _furnitureMap = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in furniturePrefabs)
        {
            if (e == null || string.IsNullOrEmpty(e.prefabName) || e.prefab == null)
                continue;

            if (!_furnitureMap.ContainsKey(e.prefabName))
            {
                _furnitureMap.Add(e.prefabName, e.prefab);
            }
            else
            {
                Debug.LogWarning("[RoomLoading] prefabName duplicado en furniturePrefabs: " + e.prefabName);
            }
        }
    }

    private GameObject GetFurniturePrefab(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;

        if (_furnitureMap != null && _furnitureMap.TryGetValue(prefabName, out var prefab))
            return prefab;

        Debug.LogWarning("[RoomLoading] No se encontró prefab para nombre: " + prefabName);
        return null;
    }

    private SurfaceBinding GetSurfaceBinding(string surfaceId)
    {
        if (string.IsNullOrEmpty(surfaceId)) return null;

        if (_surfaceMap != null && _surfaceMap.TryGetValue(surfaceId, out var sb))
            return sb;

        Debug.LogWarning("[RoomLoading] No se encontró SurfaceBinding para surfaceId: " + surfaceId);
        return null;
    }

    #endregion

    #region Aplicar texturas

    private void ApplyTextures(RoomSaveData data)
    {
        if (data.textures == null || data.textures.Count == 0)
        {
            Debug.Log("[RoomLoading] No hay texturas en el JSON para aplicar.");
            return;
        }

        foreach (var t in data.textures)
        {
            if (t == null || string.IsNullOrEmpty(t.surfaceId)) continue;

            var binding = GetSurfaceBinding(t.surfaceId);
            if (binding == null || binding.renderer == null)
                continue;

            // Aquí es donde conectas TU pipeline de PBR.
            // t.path es el directorio del pack PBR (ej: .../pbrpacks/Plaster001_2K-PNG).
            // Puedes cargar las texturas (albedo, normal, roughness, etc.) desde ese path
            // y asignarlas al material del renderer.

            ApplyPbrPackToRenderer(binding.renderer, t.path, t.pack);
        }
    }

    /// <summary>
    /// Stub donde conectas tu lógica real de carga de PBR.
    /// </summary>
    private void ApplyPbrPackToRenderer(Renderer renderer, string packPath, string packName)
    {
        if (renderer == null)
            return;

        Debug.Log($"[RoomLoading] (TODO) Aplicar PBR pack='{packName}' desde '{packPath}' a '{renderer.gameObject.name}'");

        // TODO:
        // - Usar tus métodos existentes para cargar texturas desde packPath.
        // - Asignarlas al material del renderer.
        //
        // Ejemplo muy simplificado (si tuvieras una clase estática):
        // PbrLoader.ApplyPack(renderer, packPath);
    }

    #endregion

    #region Reconstruir muebles

    private void RebuildFurniture(RoomSaveData data)
    {
        // 1. Borrar muebles actuales
        ClearExistingFurniture();

        if (data.items == null || data.items.Count == 0)
        {
            Debug.Log("[RoomLoading] No hay muebles en el JSON.");
            return;
        }

        foreach (var item in data.items)
        {
            if (item == null || string.IsNullOrEmpty(item.prefabName))
                continue;

            var prefab = GetFurniturePrefab(item.prefabName);
            if (prefab == null)
                continue;

            var instance = Instantiate(prefab, item.position, item.rotation);
            instance.transform.localScale = item.scale;

            // Asegurar el tag "Furniture" para futuras guardadas
            instance.tag = "Furniture";
        }
    }

    private void ClearExistingFurniture()
    {
        var existing = GameObject.FindGameObjectsWithTag("Furniture");
        foreach (var go in existing)
        {
            Destroy(go);
        }
    }

    #endregion
}

#region Clases auxiliares para el inspector

[Serializable]
public class SurfaceBinding
{
    [Tooltip("ID lógico de la superficie, debe coincidir con surfaceId de SurfaceTextureData (ej: 'Pared de B a C (north)', 'floor', 'ceiling').")]
    public string surfaceId;

    [Tooltip("Renderer al que se le aplicará la textura PBR.")]
    public Renderer renderer;
}

[Serializable]
public class FurniturePrefabEntry
{
    [Tooltip("Nombre del prefab tal como aparece en prefabName dentro del JSON.")]
    public string prefabName;

    [Tooltip("Prefab real en Unity que se instanciará.")]
    public GameObject prefab;
}

#endregion
