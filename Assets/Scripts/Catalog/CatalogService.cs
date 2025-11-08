using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Roomify.Catalog
{
    [Serializable]
    public class CatalogItemSize { public float w, h, d; }

    [Serializable]
    public class CatalogItem
    {
        public string id;
        public string name;
        public string category;
        public string prefabPath;     // ruta dentro de Resources (ej: "Prefabs/SofaChesterfield")
        public string thumbnail;      // opcional: ruta dentro de Resources (ej: "thumbnails/sofa")
        public CatalogItemSize sizeMeters;
        public string[] tags;
        public float placementScale = 1f;
    }

    [Serializable]
    public class CatalogData
    {
        public string version;
        public string updatedAt;
        public List<CatalogItem> items = new List<CatalogItem>();
    }

    public static class CatalogService
    {
        public static CatalogData Data { get; private set; }
        public static bool IsLoaded => Data != null && Data.items != null;

        public static async Task LoadAsync(string fileName = "catalog.json")
        {
            string path = Path.Combine(Application.streamingAssetsPath, fileName);

#if UNITY_ANDROID && !UNITY_EDITOR
            using (UnityWebRequest www = UnityWebRequest.Get(path))
            {
                await www.SendWebRequest();
                if (www.result != UnityWebRequest.Result.Success)
                    throw new Exception($"Error leyendo StreamingAssets: {www.error}");

                string json = www.downloadHandler.text;
                Data = JsonUtility.FromJson<CatalogData>(json);
            }
#else
            if (!File.Exists(path))
                throw new FileNotFoundException($"No existe {path}");

            string json = File.ReadAllText(path);
            Data = JsonUtility.FromJson<CatalogData>(json);
#endif
            if (Data == null) throw new Exception("No se pudo parsear el catálogo");

            Debug.Log($"✅ Catálogo cargado: {Data.items.Count} items (v{Data.version})");
        }

        public static IEnumerable<CatalogItem> GetByCategory(string category)
        {
            if (!IsLoaded) yield break;
            foreach (var it in Data.items)
                if (string.Equals(it.category, category, StringComparison.OrdinalIgnoreCase))
                    yield return it;
        }

        public static CatalogItem GetById(string id)
        {
            if (!IsLoaded) return null;
            return Data.items.Find(i => i.id == id);
        }

        public static GameObject LoadPrefab(CatalogItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.prefabPath)) return null;
            var prefab = Resources.Load<GameObject>(item.prefabPath);
            if (prefab == null)
                Debug.LogError($"❌ Prefab no encontrado en Resources: {item.prefabPath}");
            return prefab;
        }

        public static Sprite LoadThumbnail(CatalogItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.thumbnail)) return null;
            return Resources.Load<Sprite>(item.thumbnail);
        }
    }
}
