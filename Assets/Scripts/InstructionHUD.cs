using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InstructionHUD : MonoBehaviour
{
    [Header("Panel raíz")]
    public GameObject panelRoot;

    [Header("UI")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI bodyText;
    public Button toggleButton;

    private bool visible = true;

    void Awake()
    {
        if (toggleButton != null)
            toggleButton.onClick.AddListener(Toggle);
    }

    public void Toggle()
    {
        SetVisible(!visible);
    }

    public void SetVisible(bool show)
    {
        visible = show;
        if (panelRoot != null) panelRoot.SetActive(visible);
    }

    public void SetModeAltura()
    {
        if (titleText) titleText.text = "Instrucciones - Medir altura";
        if (bodyText) bodyText.text =
            "1) Apunta al piso y coloca el PUNTO INFERIOR.\n" +
            "2) Apunta al techo o al punto superior y coloca el PUNTO SUPERIOR.\n" +
            "3) Presiona CONFIRMAR para guardar la altura y pasar a medir el piso.\n\n" +
            "Botones:\n" +
            "• Confirmar: guarda la medición actual.\n" +
            "• Eliminar: borra lo hecho desde la última confirmación.";
    }

    public void SetModePiso()
    {
        if (titleText) titleText.text = "Instrucciones - Medir piso";
        if (bodyText) bodyText.text =
            "1) Coloca las ESQUINAS del cuarto en orden (mínimo 3 puntos).\n" +
            "2) Verás la distancia entre cada par de puntos.\n" +
            "3) Presiona CONFIRMAR para cerrar la figura y exportar el JSON.\n\n" +
            "Botones:\n" +
            "• Confirmar: cierra figura y exporta JSON.\n" +
            "• Eliminar: borra lo hecho desde la última confirmación.";
    }

    public void OnHeightPointsChanged(int count)
    {
        if (!bodyText) return;
        string baseTxt =
            "1) Apunta al piso y coloca el PUNTO INFERIOR.\n" +
            "2) Apunta al techo o al punto superior y coloca el PUNTO SUPERIOR.\n" +
            "3) Presiona CONFIRMAR para guardar la altura y pasar a medir el piso.\n\n" +
            "Progreso: ";
        if (count <= 0) bodyText.text = baseTxt + "0/2 puntos colocados.";
        else if (count == 1) bodyText.text = baseTxt + "1/2 puntos colocados.";
        else bodyText.text = baseTxt + "2/2 puntos colocados. ¡Confirma!";
    }

    public void OnFloorPointsChanged(int count)
    {
        if (!bodyText) return;
        bodyText.text =
            "1) Coloca las ESQUINAS del cuarto en orden (mínimo 3 puntos).\n" +
            "2) Verás la distancia entre cada par de puntos.\n" +
            "3) Presiona CONFIRMAR para cerrar la figura y exportar el JSON.\n\n" +
            $"Progreso: {count} punto(s) colocados.";
    }

    public void ShowExportResult(string path)
    {
        if (!titleText || !bodyText) return;
        titleText.text = "Exportación completada";
        bodyText.text =
            "El archivo JSON fue generado correctamente.\n\n" +
            $"Ruta:\n{path}\n\n" +
            "Puedes iniciar una nueva medición si lo deseas.";
    }
}
