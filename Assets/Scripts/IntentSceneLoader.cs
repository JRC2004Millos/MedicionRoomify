#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
using UnityEngine;
using System.IO;

public class IntentSceneLoader : MonoBehaviour
{
    const string EXTRA_SCENE = "SCENE_TO_LOAD";
    const string EXTRA_PBR = "PBR_PACKS_ROOT";

    static string sceneToLoad;
    static string texturesJsonPath;
    static string pbrPacksRoot;

    void Awake()
    {
        ReadExtras(true);
        Route(sceneToLoad);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            ReadExtras(false);
            Route(sceneToLoad);
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            ReadExtras(false);
            Route(sceneToLoad);
        }
    }

    void ReadExtras(bool logHeader)
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
                var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                var intent      = activity.Call<AndroidJavaObject>("getIntent");
                sceneToLoad     = intent.Call<string>("getStringExtra", EXTRA_SCENE) ?? sceneToLoad;
                pbrPacksRoot    = intent.Call<string>("getStringExtra", EXTRA_PBR)   ?? pbrPacksRoot;
        #endif
                if (logHeader) Debug.Log($"[Bootstrap] SCENE_TO_LOAD='{sceneToLoad}'");
                Debug.Log($"[Bridge] TEXTURES_JSON_PATH={texturesJsonPath}");
                Debug.Log($"[Bridge] PBR_PACKS_ROOT={pbrPacksRoot}");
    }

    public static string GetTexturesJsonPath() => texturesJsonPath;
    public static string GetPbrPacksRoot()     => pbrPacksRoot;

    void Route(string scene)
    {
        if (string.IsNullOrEmpty(scene)) return;
        StartCoroutine(SceneHandoff.Go(scene));
    }
}
