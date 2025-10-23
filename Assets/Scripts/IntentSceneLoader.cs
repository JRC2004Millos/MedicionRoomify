using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class IntentSceneLoader : MonoBehaviour
{
    [SerializeField] string defaultScene = "RenderScene";
    bool _routed = false;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        TryRouteOnce();
    }

    void OnApplicationFocus(bool hasFocus)  { TryRouteOnce(); }
    void OnApplicationPause(bool paused)    { if (!paused) TryRouteOnce(); }

    void TryRouteOnce()
    {
        if (_routed) return;

        string sceneFromIntent = null;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
            {
                sceneFromIntent = intent.Call<string>("getStringExtra", "SCENE_TO_LOAD");

                // Limpia el extra para evitar handoff repetidos cuando vuelva foco/pausa
                if (sceneFromIntent != null)
                    intent.Call<AndroidJavaObject>("removeExtra", "SCENE_TO_LOAD");
            }
        }
        catch { /* ignora, usa default */ }
#endif
        var target = string.IsNullOrEmpty(sceneFromIntent) ? defaultScene : sceneFromIntent;
        StartCoroutine(Go(target));
        _routed = true;
    }

    IEnumerator Go(string sceneName)
    {
        // opcional: pantalla de transición aquí
        var async = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (!async.isDone) yield return null;

        // Bootstrap ya no es necesario
        Destroy(gameObject);
    }
}
