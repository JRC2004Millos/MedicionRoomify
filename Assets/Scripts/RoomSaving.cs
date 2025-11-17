using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class RoomSaving : MonoBehaviour
{
    [Header("Identificador interno de la habitación")]
    public string roomId = "habitacion_001";

    [Header("Nombre visible del espacio")]
    public string spaceName = "";

    [Header("Nombre del archivo (opcional)")]
    public string saveFileName = "";

    [Header("Objetos a guardar")]
    [Tooltip("Nombre de la Tag que usan los muebles colocados en la escena (deja vacío para no guardar objetos).")]
    public string furnitureTag = "Furniture";

    private const string ROOM_DATA_FILE_NAME      = "room_data.json";
    private const string TEXTURES_MODEL_FILE_NAME = "textures_model.json";


    public void SaveRoom()
    {
        FinalRoomModel data = new FinalRoomModel
        {
            roomId    = roomId,
            spaceName = string.IsNullOrEmpty(spaceName) ? roomId : spaceName,
            geometry  = null,
            textures  = new List<CombinedTextureData>(),
            items     = new List<FurnitureItemData>()
        };

        string geoPath = Path.Combine(Application.persistentDataPath, ROOM_DATA_FILE_NAME);
        if (File.Exists(geoPath))
        {
            try
            {
                string geoText = File.ReadAllText(geoPath);
                RoomData geo = JsonUtility.FromJson<RoomData>(geoText);
                data.geometry = geo;
            }
            catch (Exception ex)
            {
                Debug.LogError("[RoomSaving] Error leyendo room_data.json: " + ex.Message);
            }
        }
        else
        {
            Debug.LogWarning("[RoomSaving] No se encontró room_data.json en: " + geoPath);
        }

        string texPath = Path.Combine(Application.persistentDataPath, TEXTURES_MODEL_FILE_NAME);
        if (File.Exists(texPath))
        {
            try
            {
                string texText = File.ReadAllText(texPath);
                TextureModelRoot texRoot = JsonUtility.FromJson<TextureModelRoot>(texText);

                if (texRoot != null && texRoot.items != null)
                {
                    foreach (var it in texRoot.items)
                    {
                        data.textures.Add(new CombinedTextureData
                        {
                            wall = it.wall,
                            pack = it.pack,
                            path = it.path
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[RoomSaving] Error al leer textures_model.json: " + ex.Message);
            }
        }
        else
        {
            Debug.LogWarning("[RoomSaving] No se encontró textures_model.json en: " + texPath);
        }

        GameObject[] furnitureObjects = Array.Empty<GameObject>();

        if (!string.IsNullOrEmpty(furnitureTag))
        {
            try
            {
                furnitureObjects = GameObject.FindGameObjectsWithTag(furnitureTag);
            }
            catch (UnityException ex)
            {
                Debug.LogWarning($"[RoomSaving] La tag '{furnitureTag}' no existe. No se guardarán muebles. Detalle: {ex.Message}");
            }
        }

        Debug.Log("[RoomSaving] Muebles encontrados con tag '" + furnitureTag + "': " + furnitureObjects.Length);
        foreach (var go in furnitureObjects)
        {
            FurnitureItemData item = new FurnitureItemData();

            string prefabName = go.name;
            int cloneIndex = prefabName.IndexOf("(Clone)", StringComparison.Ordinal);
            if (cloneIndex > 0)
                prefabName = prefabName.Substring(0, cloneIndex);

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

        string finalPath = Path.Combine(baseDir, fileName);

        try
        {
            File.WriteAllText(finalPath, json);
            Debug.Log($"[RoomSaving] Modelo guardado en:\n{finalPath}\nJSON:\n{json}");
        }
        catch (Exception e)
        {
            Debug.LogError("[RoomSaving] Error al guardar JSON: " + e.Message);
        }
    }
}

#region --- CLASES SERIALIZABLES ---

[Serializable]
public class CombinedTextureData
{
    public string wall;
    public string pack;
    public string path;
}

[Serializable]
public class TextureModelRoot
{
    public string project;
    public List<CombinedTextureData> items;
}

[Serializable]
public class FurnitureItemData
{
    public string    prefabName;
    public Vector3   position;
    public Quaternion rotation;
    public Vector3   scale;
}

[Serializable]
public class FinalRoomModel
{
    public string roomId;
    public string spaceName;
    public RoomData geometry;
    public List<CombinedTextureData> textures;
    public List<FurnitureItemData> items;
}

#endregion