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

    [Header("Ruta del layout actualmente cargado (opcional)")]
    public string currentLayoutPath = "";

    [Header("Nombre del archivo (opcional)")]
    public string saveFileName = "";

    [Header("Objetos a guardar")]
    [Tooltip("Nombre de la Tag que usan los muebles colocados en la escena (deja vacío para no guardar objetos).")]
    public string furnitureTag = "Furniture";

    private const string ROOM_DATA_FILE_NAME = "room_data.json";
    private const string TEXTURES_MODEL_FILE_NAME = "textures_model.json";


    public void SaveRoom()
    {
        // --- 1) Rutas base ---
        string geoPath = Path.Combine(Application.persistentDataPath, ROOM_DATA_FILE_NAME);
        string texPath = Path.Combine(Application.persistentDataPath, TEXTURES_MODEL_FILE_NAME);

        string baseDir = Path.Combine(Application.persistentDataPath, "Modelos");
        if (!Directory.Exists(baseDir))
            Directory.CreateDirectory(baseDir);

        string fileName = string.IsNullOrEmpty(saveFileName)
            ? $"{roomId}_layout.json"
            : saveFileName;

        string finalPath = Path.Combine(baseDir, fileName);

        bool hasGeo = File.Exists(geoPath);
        bool hasTex = File.Exists(texPath);

        FinalRoomModel data = null;

        if (!hasGeo && !hasTex)
        {
            string sourcePath = null;

            if (!string.IsNullOrEmpty(currentLayoutPath) && File.Exists(currentLayoutPath))
            {
                sourcePath = currentLayoutPath;
            }
            else if (File.Exists(finalPath))
            {
                sourcePath = finalPath;
            }

            if (!string.IsNullOrEmpty(sourcePath))
            {
                try
                {
                    string existingJson = File.ReadAllText(sourcePath);
                    data = JsonUtility.FromJson<FinalRoomModel>(existingJson);
                    Debug.Log("[RoomSaving] Inicializando modelo desde layout existente: " + sourcePath);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[RoomSaving] No se pudo leer el layout existente. Se creará uno nuevo. Detalle: " + ex.Message);
                    data = null;
                }
            }
        }

        if (data == null)
        {
            data = new FinalRoomModel
            {
                roomId = roomId,
                spaceName = string.IsNullOrEmpty(spaceName) ? roomId : spaceName,
                geometry = null,
                textures = new List<CombinedTextureData>(),
                items = new List<FurnitureItemData>()
            };
        }

        if (!string.IsNullOrEmpty(roomId))
            data.roomId = roomId;

        if (!string.IsNullOrEmpty(spaceName))
            data.spaceName = spaceName;
        else if (string.IsNullOrEmpty(data.spaceName))
            data.spaceName = string.IsNullOrEmpty(roomId) ? "espacio_sin_nombre" : roomId;

        if (hasGeo)
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
            if (data.geometry == null)
                Debug.LogWarning("[RoomSaving] No hay room_data.json y el layout previo no tenía geometry. geometry quedará nulo.");
        }

        if (hasTex)
        {
            try
            {
                string texText = File.ReadAllText(texPath);
                TextureModelRoot texRoot = JsonUtility.FromJson<TextureModelRoot>(texText);

                if (texRoot != null && texRoot.items != null)
                {
                    if (data.textures == null)
                        data.textures = new List<CombinedTextureData>();

                    var map = new Dictionary<string, CombinedTextureData>(StringComparer.OrdinalIgnoreCase);

                    foreach (var existing in data.textures)
                    {
                        if (existing == null || string.IsNullOrEmpty(existing.wall)) continue;
                        if (!map.ContainsKey(existing.wall))
                            map[existing.wall] = existing;
                    }

                    foreach (var it in texRoot.items)
                    {
                        if (it == null || string.IsNullOrEmpty(it.wall)) continue;

                        map[it.wall] = new CombinedTextureData
                        {
                            wall = it.wall,
                            pack = it.pack,
                            path = it.path
                        };
                    }

                    data.textures = new List<CombinedTextureData>(map.Values);

                    Debug.Log($"[RoomSaving] Texturas tras merge: {data.textures.Count}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[RoomSaving] Error al leer textures_model.json: " + ex.Message);
            }
        }
        else
        {
            if (data.textures == null)
                data.textures = new List<CombinedTextureData>();
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

        data.items = new List<FurnitureItemData>();

        foreach (var go in furnitureObjects)
        {
            var item = new FurnitureItemData();

            string prefabName = go.name;
            int cloneIndex = prefabName.IndexOf("(Clone)", StringComparison.Ordinal);
            if (cloneIndex > 0)
                prefabName = prefabName.Substring(0, cloneIndex);

            item.prefabName = prefabName;
            item.position = go.transform.position;
            item.rotation = go.transform.rotation;
            item.scale = go.transform.localScale;

            data.items.Add(item);
        }

        string json = JsonUtility.ToJson(data, true);

        try
        {
            File.WriteAllText(finalPath, json);
            Debug.Log($"[RoomSaving] Modelo guardado en:\n{finalPath}\nJSON:\n{json}");

            CleanupSourceFiles(geoPath, texPath);
        }
        catch (Exception e)
        {
            Debug.LogError("[RoomSaving] Error al guardar JSON: " + e.Message);
        }
    }

    private void CleanupSourceFiles(string geoPath, string texPath)
    {
        try
        {
            if (File.Exists(geoPath))
            {
                File.Delete(geoPath);
                Debug.Log("[RoomSaving] room_data.json borrado: " + geoPath);
            }

            if (File.Exists(texPath))
            {
                File.Delete(texPath);
                Debug.Log("[RoomSaving] textures_model.json borrado: " + texPath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[RoomSaving] No se pudieron borrar los archivos fuente: " + ex.Message);
        }
    }
}

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
    public string prefabName;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
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