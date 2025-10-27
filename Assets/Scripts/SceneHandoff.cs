using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneHandoff : MonoBehaviour
{
    private static SceneHandoff _instance;

    private static SceneHandoff GetOrCreate()
    {
        if (_instance != null) return _instance;
        var go = new GameObject("[SceneHandoff]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SceneHandoff>();
        return _instance;
    }

    public static IEnumerator Go(string sceneName)
    {
        yield return null;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        yield return op;
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        yield return new WaitForEndOfFrame();

        System.GC.Collect();
        yield return Resources.UnloadUnusedAssets();

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        op.allowSceneActivation = true;
        while (!op.isDone)
            yield return null;
    }
}