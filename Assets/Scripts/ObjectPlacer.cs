using UnityEngine;

public class ObjectPlacer : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera mainCamera;

    [Header("Placement Settings")]
    [SerializeField] private float maxRaycastDistance = 50f;
    [SerializeField] private float defaultPlacementDistance = 2f;
    [SerializeField] private LayerMask placementLayerMask = -1; // Todo por defecto

    [Header("Preview Settings")]
    [SerializeField] private Color validColor = new Color(0, 1, 0, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1, 1, 0, 0.5f);

    [Header("Furniture Settings")]
    [Tooltip("Nombre de la capa donde deben quedar los muebles colocados")]
    [SerializeField] private string furnitureLayerName = "Furniture";

    [Tooltip("Si está activo, obliga a que el mueble tenga al menos un BoxCollider")]
    [SerializeField] private bool forceBoxCollider = true;

    // Estado actual
    private GameObject currentPrefab;
    private GameObject previewObject;
    private bool isPlacementMode = false;
    private Vector3 lastValidPosition;
    private Quaternion lastValidRotation;
    private bool hasValidPlacement = false;

    public static ObjectPlacer Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void Update()
    {
        if (!isPlacementMode || previewObject == null)
            return;

        UpdatePreviewPosition();

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Ended && hasValidPlacement)
            {
                PlaceObject();
            }
        }
        else if (Input.GetMouseButtonDown(0) && hasValidPlacement)
        {
            PlaceObject();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
        }
    }

    public void BeginPlacement(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("[ObjectPlacer] Prefab es null");
            return;
        }

        Debug.Log($"[ObjectPlacer] Iniciando colocación de: {prefab.name}");

        if (previewObject != null)
            Destroy(previewObject);

        currentPrefab = prefab;
        isPlacementMode = true;

        CreatePreview();
    }

    public void CancelPlacement()
    {
        Debug.Log("[ObjectPlacer] Colocación cancelada");

        isPlacementMode = false;

        if (previewObject != null)
            Destroy(previewObject);

        currentPrefab = null;
        hasValidPlacement = false;
    }

    void CreatePreview()
    {
        previewObject = Instantiate(currentPrefab);
        previewObject.name = $"Preview_{currentPrefab.name}";

        MakeTransparent(previewObject, validColor);

        var colliders = previewObject.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
            col.enabled = false;

        var rigidbodies = previewObject.GetComponentsInChildren<Rigidbody>();
        foreach (var rb in rigidbodies)
            rb.isKinematic = true;
    }

    void UpdatePreviewPosition()
    {
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Ray ray = mainCamera.ScreenPointToRay(screenCenter);

        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxRaycastDistance, placementLayerMask))
        {
            lastValidPosition = hit.point;
            lastValidRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            hasValidPlacement = true;

            previewObject.transform.SetPositionAndRotation(lastValidPosition, lastValidRotation);
            SetPreviewColor(validColor);

            Debug.DrawLine(ray.origin, hit.point, Color.green);
        }
        else
        {
            lastValidPosition = mainCamera.transform.position + mainCamera.transform.forward * defaultPlacementDistance;
            lastValidRotation = Quaternion.identity;
            hasValidPlacement = true;

            previewObject.transform.SetPositionAndRotation(lastValidPosition, lastValidRotation);
            SetPreviewColor(invalidColor);

            Debug.DrawRay(ray.origin, ray.direction * defaultPlacementDistance, Color.yellow); // Debug visual
        }
    }

    void PlaceObject()
    {
        Debug.Log($"[ObjectPlacer] Colocando objeto en: {lastValidPosition}");

        GameObject placedObject = Instantiate(currentPrefab, lastValidPosition, lastValidRotation);
        placedObject.name = currentPrefab.name;

        SetupPlacedFurniture(placedObject);

        CancelPlacement();
    }

    void SetupPlacedFurniture(GameObject root)
    {
        int furnitureLayer = LayerMask.NameToLayer(furnitureLayerName);
        if (furnitureLayer != -1)
        {
            SetLayerRecursively(root, furnitureLayer);
        }

        root.tag = "Furniture";

        var meshColliders = root.GetComponentsInChildren<MeshCollider>(true);
        foreach (var mc in meshColliders) Destroy(mc);

        if (forceBoxCollider)
        {
            var allColliders = root.GetComponentsInChildren<Collider>(true);
            if (allColliders == null || allColliders.Length == 0)
            {
                var rend = root.GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    Bounds b = rend.bounds;
                    BoxCollider box = root.AddComponent<BoxCollider>();
                    box.center = root.transform.InverseTransformPoint(b.center);
                    box.size = b.size;
                }
                else
                {
                    root.AddComponent<BoxCollider>();
                }
            }
        }
    }

    bool IsTagDefined(string tag)
    {
        try
        {
            var t = GameObject.FindGameObjectsWithTag(tag);
            return true;
        }
        catch
        {
            return false;
        }
    }
    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    void MakeTransparent(GameObject obj, Color color)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            Material[] newMats = new Material[rend.materials.Length];
            for (int i = 0; i < newMats.Length; i++)
            {
                newMats[i] = new Material(rend.materials[i]);
                newMats[i].color = color;

                if (newMats[i].HasProperty("_Mode"))
                {
                    newMats[i].SetFloat("_Mode", 3);
                }
                if (newMats[i].HasProperty("_SrcBlend"))
                {
                    newMats[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    newMats[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                }
                if (newMats[i].HasProperty("_ZWrite"))
                {
                    newMats[i].SetInt("_ZWrite", 0);
                }

                newMats[i].renderQueue = 3000;
            }
            rend.materials = newMats;
        }
    }

    void SetPreviewColor(Color color)
    {
        if (previewObject == null) return;

        var renderers = previewObject.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            foreach (var mat in rend.materials)
            {
                if (mat.HasProperty("_Color"))
                    mat.color = color;
            }
        }
    }

    public bool IsPlacing() => isPlacementMode;
    public GameObject GetCurrentPrefab() => currentPrefab;
}
