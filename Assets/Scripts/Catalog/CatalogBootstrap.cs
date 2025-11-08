using System.Threading.Tasks;
using UnityEngine;
using Roomify.Catalog;

public class CatalogBootstrap : MonoBehaviour
{
    [Header("Auto Instanciar para pruebas")]
    public bool instantiateOnePerCategoryOnStart = true;

    private async void Awake()
    {
        // Carga catálogo
        try
        {
            await CatalogService.LoadAsync("catalog.json");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error cargando catálogo: {e.Message}");
            return;
        }

        // Demo opcional: Instanciar uno por categoría
        if (instantiateOnePerCategoryOnStart)
        {
            string[] cats = new[] { "Mueble", "Computador", "Libro", "Silla" };
            float x = 0f;
            foreach (var cat in cats)
            {
                foreach (var item in CatalogService.GetByCategory(cat))
                {
                    var prefab = CatalogService.LoadPrefab(item);
                    if (prefab == null) continue;

                    var go = Instantiate(prefab, new Vector3(x, 0, 1.5f), Quaternion.identity);
                    go.name = item.name;
                    go.transform.localScale *= Mathf.Max(0.01f, item.placementScale);

                    Debug.Log($"🧩 Instanciado: {item.name} ({item.id})");
                    break; // uno por categoría
                }
                x += 0.8f;
            }
        }
    }
}
