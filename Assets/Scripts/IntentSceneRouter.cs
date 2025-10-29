#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
using UnityEngine;
using System.IO;

public class IntentSceneLoader : MonoBehaviour
{
    const string EXTRA_SCENE = "SCENE_TO_LOAD";
    const string EXTRA_PBR   = "PBR_PACKS_ROOT";

    static string sceneToLoad;
    static string texturesJsonPath;
    static string pbrPacksRoot;

    static IntentSceneLoader I;

     void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);

        ReadExtras(true);
        RouteWithFallback();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            ReadExtras(false);
            RouteWithFallback();
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            ReadExtras(false);
            RouteWithFallback();
        }
    }

    void ReadExtras(bool logHeader)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        var intent      = activity.Call<AndroidJavaObject>("getIntent");
        string newScene = intent.Call<string>("getStringExtra", EXTRA_SCENE);
        string newPbr   = intent.Call<string>("getStringExtra", EXTRA_PBR);

        if (!string.IsNullOrEmpty(newScene))
            sceneToLoad = newScene;
        if (!string.IsNullOrEmpty(newPbr))
            pbrPacksRoot = newPbr;

        // guardar última escena solicitada por Android
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            PlayerPrefs.SetString("SCENE_TO_LOAD", sceneToLoad);
            PlayerPrefs.SetInt("ForceBootstrap", 1);
            PlayerPrefs.Save();
        }
#endif

        if (logHeader) Debug.Log($"[Bootstrap] SCENE_TO_LOAD='{sceneToLoad}'");
        Debug.Log($"[Bridge] TEXTURES_JSON_PATH={texturesJsonPath}");
        Debug.Log($"[Bridge] PBR_PACKS_ROOT={pbrPacksRoot}");
    }

    public static string GetTexturesJsonPath() => texturesJsonPath;
    public static string GetPbrPacksRoot()     => pbrPacksRoot;

    void RouteWithFallback()
    {
        string scene = sceneToLoad;

        // 1️⃣ Si no hay intent válido, revisa PlayerPrefs
        if (string.IsNullOrEmpty(scene))
        {
            bool force = PlayerPrefs.GetInt("ForceBootstrap", 0) == 1;
            if (force)
                scene = PlayerPrefs.GetString("SCENE_TO_LOAD", "SampleScene");
        }

        if (string.IsNullOrEmpty(scene))
            scene = "SampleScene"; // fallback total

        // 2️⃣ limpiar banderas
        PlayerPrefs.DeleteKey("SCENE_TO_LOAD");
        PlayerPrefs.SetInt("ForceBootstrap", 0);
        PlayerPrefs.Save();

        // 3️⃣ redirigir
        Debug.Log($"[BootstrapRouter] Routing to: {scene}");
        StartCoroutine(SceneHandoff.Go(scene));
    }
}
