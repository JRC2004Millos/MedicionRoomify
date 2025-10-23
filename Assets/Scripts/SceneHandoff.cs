using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ARFOUNDATION_PRESENT
using UnityEngine.XR.ARFoundation;
#endif

// ⚠️ Sin namespace, para que sea visible globalmente
public class SceneHandoff : MonoBehaviour
{
    private static SceneHandoff _instance;
    public static SceneHandoff Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("__SceneHandoff");
                _instance = go.AddComponent<SceneHandoff>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    /// <summary>
    /// Cambia de escena de forma segura, cerrando AR y liberando memoria antes del cambio.
    /// </summary>
    public static void To(string target)
    {
        Instance.StartCoroutine(Instance.Go(target));
    }

    public IEnumerator Go(string target)
    {
        Debug.Log($"[SceneHandoff] Transición segura hacia '{target}'");

#if ARFOUNDATION_PRESENT
        // Apaga componentes AR activos
        var arSession       = FindObjectOfType<ARSession>();
        var arCameraManager = FindObjectOfType<ARCameraManager>();
        var planeManager    = FindObjectOfType<ARPlaneManager>();
        var pointCloud      = FindObjectOfType<ARPointCloudManager>();
        var raycastMgr      = FindObjectOfType<ARRaycastManager>();

        if (raycastMgr)      raycastMgr.enabled = false;
        if (planeManager)    planeManager.enabled = false;
        if (pointCloud)      pointCloud.enabled = false;
        if (arCameraManager) arCameraManager.enabled = false;
        if (arSession)       arSession.enabled = false;
#endif

        yield return new WaitForEndOfFrame();
        yield return Resources.UnloadUnusedAssets();
        System.GC.Collect();

        var op = SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
        op.allowSceneActivation = true;
        yield return op;
    }
}
