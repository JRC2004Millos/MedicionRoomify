#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public static class ObjToPrefabImporter
{
    [MenuItem("Roomify/Prefabs/Crear prefabs desde Models (.obj)")]
    public static void CreatePrefabsFromModels()
    {
        string modelsFolder = "Assets/Models";
        string prefabsFolder = "Assets/Resources/Prefabs";
        if (!Directory.Exists(modelsFolder))
        {
            Debug.LogError("❌ No existe Assets/Models. Copia ahí tus .obj.");
            return;
        }
        if (!Directory.Exists(prefabsFolder))
            Directory.CreateDirectory(prefabsFolder);

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { modelsFolder });
        int created = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) continue;

            string name = Path.GetFileNameWithoutExtension(path);
            string target = Path.Combine(prefabsFolder, name + ".prefab").Replace("\\", "/");

            // Ya existe un prefab con ese nombre
            if (File.Exists(target)) continue;

            // Crear instancia temporal y guardarla como prefab
            var temp = PrefabUtility.InstantiatePrefab(model) as GameObject;
            temp.name = name;

            // Ajustes opcionales por defecto (rotación/escala si tus .obj vienen muy grandes/pequeños)
            // temp.transform.localScale = Vector3.one;

            PrefabUtility.SaveAsPrefabAsset(temp, target);
            Object.DestroyImmediate(temp);
            created++;
            Debug.Log($"🧱 Prefab creado: {target}");
        }

        AssetDatabase.Refresh();
        Debug.Log($"✅ Prefabs nuevos: {created} (carpeta: {prefabsFolder})");
    }
}
#endif
