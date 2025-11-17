using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RoomLoading : MonoBehaviour
{
    [Header("Ruta de layout (opcional)")]
    public string layoutOverridePath = "";

    [Header("Superficies presentes en la escena")]
    public List<SurfaceBinding> sceneSurfaces = new List<SurfaceBinding>();

    [Header("Prefabs de muebles disponibles")]
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
        FinalRoomModel data = null;
        try
        {
            data = JsonUtility.FromJson<FinalRoomModel>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("[RoomLoading] Error al parsear FinalRoomModel: " + e.Message);
            return;
        }

        if (data == null)
        {
            Debug.LogError("[RoomLoading] FinalRoomModel nulo al deserializar JSON.");
            return;
        }

        Debug.Log($"[RoomLoading] Layout cargado. roomId={data.roomId}, spaceName={data.spaceName}");

        ApplyTextures(data);
        RebuildFurniture(data);
    }

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
        return layoutOverridePath;
#endif
    }

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

    private void ApplyTextures(FinalRoomModel data)
    {
        if (data.textures == null || data.textures.Count == 0)
        {
            Debug.Log("[RoomLoading] No hay texturas en el JSON para aplicar.");
            return;
        }

        foreach (var t in data.textures)
        {
            if (t == null || string.IsNullOrEmpty(t.wall)) continue;

            var binding = GetSurfaceBinding(t.wall);
            if (binding == null || binding.renderer == null)
                continue;

            ApplyPbrPackToRenderer(binding.renderer, t.path, t.pack);
        }
    }

    private void ApplyPbrPackToRenderer(Renderer renderer, string packPath, string packName)
    {
        if (renderer == null)
            return;

        Debug.Log($"[RoomLoading] Aplicar PBR pack='{packName}' desde '{packPath}' a '{renderer.gameObject.name}'");
    }

    private void RebuildFurniture(FinalRoomModel data)
    {
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
}

[Serializable]
public class SurfaceBinding
{
    public string surfaceId;
    public Renderer renderer;
}

[Serializable]
public class FurniturePrefabEntry
{
    public string prefabName;
    public GameObject prefab;
}