using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RoomSaving : MonoBehaviour
{
    [Header("Identificador interno de la habitación")]
    [Tooltip("Puedes usar un ID interno (ej: habitacion_001).")]
    public string roomId = "habitacion_001";

    [Header("Nombre visible del espacio")]
    [Tooltip("Nombre que escribió el usuario (Sala, Cuarto, Oficina, etc.)")]
    public string spaceName = "";

    [Header("Nombre del archivo (opcional)")]
    [Tooltip("Si lo dejas vacío, se usará: {roomId}_layout.json")]
    public string saveFileName = "";

    [Header("Superficies a guardar (paredes, piso, techo)")]
    [Tooltip("Configura aquí las superficies que existen en la escena")]
    public List<SurfaceEntry> surfaceEntries = new List<SurfaceEntry>();

    public void SaveRoom()
    {
        RoomSaveData data = new RoomSaveData();

        data.roomId    = roomId;
        data.spaceName = string.IsNullOrEmpty(spaceName) ? roomId : spaceName;

        foreach (var s in surfaceEntries)
        {
            if (string.IsNullOrEmpty(s.surfaceId))
            {
                Debug.LogWarning("[RoomSaving] SurfaceEntry con surfaceId vacío, lo omito.");
                continue;
            }

            SurfaceTextureData texData = new SurfaceTextureData
            {
                surfaceId = s.surfaceId,
                pack      = s.packName,
                path      = s.localPath
            };

            data.textures.Add(texData);
        }

        GameObject[] furnitureObjects = GameObject.FindGameObjectsWithTag("Furniture");

        foreach (var go in furnitureObjects)
        {
            FurnitureItemData item = new FurnitureItemData();

            string prefabName = go.name;
            int cloneIndex = prefabName.IndexOf("(Clone)", StringComparison.Ordinal);
            if (cloneIndex > 0)
            {
                prefabName = prefabName.Substring(0, cloneIndex);
            }

            item.prefabName = prefabName;
            item.position   = go.transform.position;
            item.rotation   = go.transform.rotation;
            item.scale      = go.transform.localScale;

            data.items.Add(item);
        }

        string json = JsonUtility.ToJson(data, true);

        string fileName = string.IsNullOrEmpty(saveFileName)
            ? $"{roomId}_layout.json"
            : saveFileName;

        string baseDir = Path.Combine(Application.persistentDataPath, "Modelos");
        if (!Directory.Exists(baseDir))
            Directory.CreateDirectory(baseDir);

        string path = Path.Combine(baseDir, fileName);

        try
        {
            File.WriteAllText(path, json);
            Debug.Log($"[RoomSaving] Modelo guardado en:\n{path}\nJSON:\n{json}");
        }
        catch (Exception e)
        {
            Debug.LogError("[RoomSaving] Error al guardar JSON: " + e.Message);
        }
    }
}

[Serializable]
public class FurnitureItemData
{
    public string prefabName;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
}

[Serializable]
public class SurfaceTextureData
{
    public string surfaceId;
    public string pack;
    public string path;
}

[Serializable]
public class RoomSaveData
{
    public string roomId;
    public string spaceName;
    public List<SurfaceTextureData> textures = new List<SurfaceTextureData>();
    public List<FurnitureItemData> items     = new List<FurnitureItemData>();
}

[Serializable]
public class SurfaceEntry
{
    [Tooltip("ID de la superficie (debe coincidir con el usado en tus JSON, ej: 'Pared de B a C (north)')")]
    public string surfaceId;
    [Tooltip("Nombre del pack PBR asignado a esta superficie")]
    public string packName;
    [Tooltip("Ruta local donde está el pack PBR")]
    public string localPath;
}