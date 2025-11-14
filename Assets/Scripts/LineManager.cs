using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.IO;

public class LineManager : MonoBehaviour
{
    [Header("Refs")]
    public LineRenderer lineRenderer;
    public ARPlacementController placementController;
    public TextMeshProUGUI debugText;
    public TextMeshProUGUI TxtEstado;
    public TextMeshPro mText;
    private bool _exportando = false;

    public InstructionHUD hud;

    private List<Vector3> placedPositions = new List<Vector3>();
    private List<Corner> corners = new List<Corner>();
    private char currentCornerId = 'A';

    public enum ModoMedicion { Altura, Piso }
    public ModoMedicion modoActual = ModoMedicion.Altura;

    private List<Vector3> puntosAltura = new List<Vector3>();
    private float alturaMedida = 2.8f;

    private int placedPositionsAtConfirm = 0;
    private int cornersAtConfirm = 0;
    private char cornerIdAtConfirm = 'A';
    private List<GameObject> visualsSinceConfirm = new List<GameObject>();

    void Start()
    {
        if (placementController != null)
            placementController.OnObjectPlaced += DrawLine;

        TxtEstado?.SetText("Paso 1: Marca dos puntos para medir la altura. Luego confirma.");
        debugText?.SetText("Modo: Altura");
        hud?.SetModeAltura();
        SnapshotAtConfirm();
    }

    void OnDestroy()
    {
        if (placementController != null)
            placementController.OnObjectPlaced -= DrawLine;
    }

    void DrawLine(Vector3 newPoint)
    {
        if (modoActual == ModoMedicion.Altura)
        {
            if (puntosAltura.Count == 0)
                ClearVisualsSinceConfirm();

            if (puntosAltura.Count >= 2)
            {
                TxtEstado?.SetText("Ya marcaste 2 puntos de altura. Presiona Confirmar o Eliminar.");
                return;
            }

            puntosAltura.Add(newPoint);
            hud?.OnHeightPointsChanged(puntosAltura.Count);

            if (puntosAltura.Count == 1)
            {
                debugText?.SetText("Punto inferior de altura colocado.");
            }
            else if (puntosAltura.Count == 2)
            {
                float altura = Vector3.Distance(puntosAltura[0], puntosAltura[1]);
                alturaMedida = altura;
                debugText?.SetText($"Altura medida: {(altura * 100f):F1} cm");

                var go = new GameObject("AlturaLine");
                var lr = go.AddComponent<LineRenderer>();
                lr.material = lineRenderer.material;
                lr.widthMultiplier = lineRenderer.widthMultiplier;
                lr.positionCount = 2;
                lr.SetPosition(0, puntosAltura[0]);
                lr.SetPosition(1, puntosAltura[1]);
                visualsSinceConfirm.Add(go);

                TextMeshPro distText = Instantiate(mText);
                distText.text = $"{(altura * 100f):F1} cm";
                Vector3 mid = (puntosAltura[0] + puntosAltura[1]) * 0.5f + Vector3.right * 0.05f;
                distText.transform.position = mid;
                if (Camera.main != null)
                    distText.transform.rotation = Quaternion.LookRotation(-Camera.main.transform.forward);
                visualsSinceConfirm.Add(distText.gameObject);

                TxtEstado?.SetText("Altura lista. Presiona Confirmar para pasar a medir el piso.");
            }

            return;
        }

        placedPositions.Add(newPoint);
        debugText?.SetText($"Punto añadido: {newPoint}");
        hud?.OnFloorPointsChanged(placedPositions.Count);

        corners.Add(new Corner(currentCornerId.ToString(), newPoint));
        currentCornerId++;

        lineRenderer.positionCount++;
        lineRenderer.SetPosition(lineRenderer.positionCount - 1, newPoint);

        if (lineRenderer.positionCount > 1)
        {
            Vector3 pointA = lineRenderer.GetPosition(lineRenderer.positionCount - 2);
            Vector3 pointB = lineRenderer.GetPosition(lineRenderer.positionCount - 1);
            float dist = Vector3.Distance(pointA, pointB);

            TextMeshPro distText = Instantiate(mText);
            distText.text = $"{(dist * 100f):F1} cm";

            Vector3 directionVector = (pointB - pointA).normalized;
            Vector3 normal = Vector3.up;
            Vector3 upward = Vector3.Cross(directionVector, -normal).normalized;

            Quaternion rotation = Quaternion.LookRotation(-normal, upward);
            Vector3 midPoint = pointA + directionVector * 0.5f;
            Vector3 elevation = upward * 0.02f;

            distText.transform.position = midPoint + elevation;
            distText.transform.rotation = rotation;

            visualsSinceConfirm.Add(distText.gameObject);
        }
    }

    public void OnBtnConfirmar()
    {
        if (modoActual == ModoMedicion.Altura)
        {
            if (puntosAltura.Count < 2)
            {
                TxtEstado?.SetText("Debes marcar dos puntos para medir la altura.");
                return;
            }

            modoActual = ModoMedicion.Piso;
            TxtEstado?.SetText("Paso 2: Mide las esquinas del piso. Agrega al menos 3 puntos y confirma.");
            debugText?.SetText($"Altura confirmada: {(alturaMedida * 100f):F1} cm");
            hud?.SetModePiso();

            ClearVisualsSinceConfirm();
            puntosAltura.Clear();
            SnapshotAtConfirm();
            return;
        }

        if (modoActual == ModoMedicion.Piso)
        {
            if (_exportando) return;
            if (placedPositions.Count < 3)
            {
                TxtEstado?.SetText("Debes tener al menos 3 puntos para cerrar el cuarto.");
                return;
            }

            _exportando = true;

            CerrarFiguraVisual();
            TxtEstado?.SetText("Figura cerrada. Generando JSON...");

            string path = ExportarJSON();
            hud?.ShowExportResult(path);

            AndroidNav.GoToCapture(path);
            PlayerPrefs.SetString("SCENE_TO_LOAD", "RenderScene");
            PlayerPrefs.SetInt("ForceBootstrap", 1);
            PlayerPrefs.Save();
            TxtEstado?.SetText("JSON generado. Cerrando Unity...");

            SnapshotAtConfirm();
        }
    }

    public static class AndroidNav
    {
        public static void GoToCapture(string jsonPath)
        {
    #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    if (activity == null)
                    {
                        Debug.LogError("[AndroidNav] currentActivity es null. Fallback a ApplicationContext (no recomendado).");
                        LaunchWithAppContext(jsonPath);
                        return;
                    }

                    activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                    {
                        try
                        {
                            using (var intent = new AndroidJavaObject("android/content/Intent",
                                activity, new AndroidJavaClass("com.example.roomify.CaptureActivity")))
                            {
                                intent.Call<AndroidJavaObject>("putExtra", "ROOM_JSON_PATH", jsonPath);

                                activity.Call("startActivity", intent);
                                Debug.Log("[AndroidNav] startActivity con Activity context (misma task, Unity en pause).");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError("[AndroidNav] Error startActivity (activity): " + ex);
                            LaunchWithAppContext(jsonPath);
                        }
                    }));
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[AndroidNav] Error al obtener currentActivity: " + ex);
                LaunchWithAppContext(jsonPath);
            }
    #else
            Debug.Log("[AndroidNav] Solo Android.");
    #endif
        }

        private static void LaunchWithAppContext(string jsonPath)
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var app = activity?.Call<AndroidJavaObject>("getApplicationContext"))
                using (var intent = new AndroidJavaObject("android/content/Intent",
                    new AndroidJavaClass("android/content/Intent").GetStatic<string>("ACTION_MAIN")))
                {
                    intent.Call<AndroidJavaObject>("setClassName", "com.example.roomify", "com.example.roomify.CaptureActivity");
                    intent.Call<AndroidJavaObject>("putExtra", "ROOM_JSON_PATH", jsonPath);
                    int FLAG_ACTIVITY_NEW_TASK = new AndroidJavaClass("android/content/Intent")
                        .GetStatic<int>("FLAG_ACTIVITY_NEW_TASK");
                    intent.Call<AndroidJavaObject>("addFlags", FLAG_ACTIVITY_NEW_TASK);

                    app.Call("startActivity", intent);
                    Debug.Log("[AndroidNav] Lanzando CaptureActivity con ApplicationContext (fallback).");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[AndroidNav] Fallback con ApplicationContext falló: " + ex);
            }
        }
    }

    public void OnBtnEliminar()
    {
        ClearVisualsSinceConfirm();

        if (modoActual == ModoMedicion.Altura)
        {
            puntosAltura.Clear();
            TxtEstado?.SetText("Altura: se borraron los puntos desde la última confirmación.");
            return;
        }

        if (modoActual == ModoMedicion.Piso)
        {
            if (placedPositions.Count > placedPositionsAtConfirm)
                placedPositions.RemoveRange(placedPositionsAtConfirm, placedPositions.Count - placedPositionsAtConfirm);

            if (corners.Count > cornersAtConfirm)
                corners.RemoveRange(cornersAtConfirm, corners.Count - cornersAtConfirm);

            currentCornerId = cornerIdAtConfirm;

            lineRenderer.positionCount = 0;
            for (int i = 0; i < placedPositions.Count; i++)
            {
                lineRenderer.positionCount++;
                lineRenderer.SetPosition(lineRenderer.positionCount - 1, placedPositions[i]);
            }

            TxtEstado?.SetText("Piso: se borraron puntos y textos desde la última confirmación.");
        }
    }

    private void CerrarFiguraVisual()
    {
        if (placedPositions.Count >= 3)
        {
            var first = placedPositions[0];
            lineRenderer.positionCount++;
            lineRenderer.SetPosition(lineRenderer.positionCount - 1, first);

            Vector3 A = placedPositions[placedPositions.Count - 1];
            Vector3 B = first;
            float dist = Vector3.Distance(A, B);

            TextMeshPro distText = Instantiate(mText);
            distText.text = $"{(dist * 100f):F1} cm";

            Vector3 directionVector = (B - A).normalized;
            Vector3 normal = Vector3.up;
            Vector3 upward = Vector3.Cross(directionVector, -normal).normalized;

            Quaternion rotation = Quaternion.LookRotation(-normal, upward);
            Vector3 midPoint = A + directionVector * 0.5f;
            Vector3 elevation = upward * 0.02f;

            distText.transform.position = midPoint + elevation;
            distText.transform.rotation = rotation;

            visualsSinceConfirm.Add(distText.gameObject);

            debugText?.SetText("Figura cerrada.");
        }
    }

    public string ExportarJSON()
    {
        RoomData data = new RoomData
        {
            room_dimensions = new RoomDimensions { height = alturaMedida },
            corners = corners,
            walls = new List<Wall>(),
            obstacles = new List<Obstacle>()
        };

        for (int i = 0; i < corners.Count; i++)
        {
            int j = (i + 1) % corners.Count;

            Vector2 fromPos = corners[i].position;
            Vector2 toPos = corners[j].position;

            float distance = Vector2.Distance(fromPos, toPos);
            string dirPath = CalcularDireccion(fromPos, toPos);

            data.walls.Add(new Wall
            {
                from = corners[i].id,
                to = corners[j].id,
                distance = distance,
                direction = dirPath
            });
        }

        string json = JsonUtility.ToJson(data, true);
        string dir = Application.persistentDataPath;
        string finalPath = Path.Combine(dir, "room_data.json");

        string tmpPath   = Path.Combine(dir, "room_data.tmp");
        File.WriteAllText(tmpPath, json);
        if (File.Exists(finalPath)) File.Delete(finalPath);
        File.Move(tmpPath, finalPath);

        debugText?.SetText($"Archivo exportado:\n{finalPath}");
        Debug.Log("JSON generado en: " + finalPath);
        return finalPath;
    }

    private string CalcularDireccion(Vector2 from, Vector2 to)
    {
        Vector2 dir = (to - from).normalized;
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return dir.x > 0 ? "east" : "west";
        else
            return dir.y > 0 ? "north" : "south";
    }

    private void SnapshotAtConfirm()
    {
        placedPositionsAtConfirm = placedPositions.Count;
        cornersAtConfirm = corners.Count;
        cornerIdAtConfirm = currentCornerId;

        ClearVisualsSinceConfirm();
    }

    private void ClearVisualsSinceConfirm()
    {
        for (int i = visualsSinceConfirm.Count - 1; i >= 0; i--)
        {
            var go = visualsSinceConfirm[i];
            if (go != null) Destroy(go);
        }
        visualsSinceConfirm.Clear();
    }
}
