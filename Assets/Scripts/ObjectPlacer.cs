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

        // ⛔ Evita mover/colocar si el puntero está sobre UI
        if (InputGuard.OverUI())
            return;

        // Actualizar posición del preview
        UpdatePreviewPosition();

        // Detectar toque/click para colocar
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

    /// <summary>
    /// Inicia el modo de colocación con un prefab
    /// </summary>
    public void BeginPlacement(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("[ObjectPlacer] Prefab es null");
            return;
        }

        Debug.Log($"[ObjectPlacer] Iniciando colocación de: {prefab.name}");

        // Limpiar preview anterior
        if (previewObject != null)
            Destroy(previewObject);

        currentPrefab = prefab;
        isPlacementMode = true;

        CreatePreview();
    }

    /// <summary>
    /// Cancela el modo de colocación
    /// </summary>
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
        // Instanciar copia del prefab
        previewObject = Instantiate(currentPrefab);
        previewObject.name = $"Preview_{currentPrefab.name}";

        // Hacer semi-transparente
        MakeTransparent(previewObject, validColor);

        // Desactivar colisiones
        var colliders = previewObject.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
            col.enabled = false;

        // Desactivar rigidbodies
        var rigidbodies = previewObject.GetComponentsInChildren<Rigidbody>();
        foreach (var rb in rigidbodies)
            rb.isKinematic = true;
    }

    void UpdatePreviewPosition()
    {
        // Raycast desde el centro de la pantalla
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Ray ray = mainCamera.ScreenPointToRay(screenCenter);

        RaycastHit hit;

        // Intentar raycast en superficies
        if (Physics.Raycast(ray, out hit, maxRaycastDistance, placementLayerMask))
        {
            // Encontró una superficie
            lastValidPosition = hit.point;
            lastValidRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            hasValidPlacement = true;

            previewObject.transform.SetPositionAndRotation(lastValidPosition, lastValidRotation);
            SetPreviewColor(validColor);

            Debug.DrawLine(ray.origin, hit.point, Color.green); // Debug visual
        }
        else
        {
            // No encontró superficie, colocar a distancia fija
            lastValidPosition = mainCamera.transform.position + mainCamera.transform.forward * defaultPlacementDistance;
            lastValidRotation = Quaternion.identity;
            hasValidPlacement = true;

            previewObject.transform.SetPositionAndRotation(lastValidPosition, lastValidRotation);
            SetPreviewColor(invalidColor); // Amarillo para indicar "sin superficie"

            Debug.DrawRay(ray.origin, ray.direction * defaultPlacementDistance, Color.yellow); // Debug visual
        }
    }

    void PlaceObject()
    {
        Debug.Log($"[ObjectPlacer] Colocando objeto en: {lastValidPosition}");

        // Instanciar el objeto real
        GameObject placedObject = Instantiate(currentPrefab, lastValidPosition, lastValidRotation);
        placedObject.name = currentPrefab.name;

        // Opcional: añadir tag o componente para identificarlo después
        // placedObject.tag = "PlacedObject";

        // Salir del modo colocación
        CancelPlacement();
    }

    void MakeTransparent(GameObject obj, Color color)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            // Crear materiales temporales transparentes
            Material[] newMats = new Material[rend.materials.Length];
            for (int i = 0; i < newMats.Length; i++)
            {
                newMats[i] = new Material(rend.materials[i]);
                newMats[i].color = color;

                // Configurar transparencia
                if (newMats[i].HasProperty("_Mode"))
                {
                    newMats[i].SetFloat("_Mode", 3); // Transparent
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

    // Métodos públicos útiles
    public bool IsPlacing() => isPlacementMode;

    public GameObject GetCurrentPrefab() => currentPrefab;
}