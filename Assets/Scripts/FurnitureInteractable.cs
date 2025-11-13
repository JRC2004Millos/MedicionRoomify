using UnityEngine;

[DisallowMultipleComponent]
public class FurnitureInteractable : MonoBehaviour
{
    [Tooltip("Identificador lógico para persistencia")]
    public string furnitureId;

    [Tooltip("Capa de raycast (poner en 'Furniture')")]
    public LayerMask selectableLayer;

    // (Opcional) highlight suave al seleccionar
    public void SetHighlight(bool on)
    {
        // puedes alternar un Outline/Material Emission aquí
        // o activar un child "SelectedFrame"
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            r.material.SetFloat("_Selected", on ? 1f : 0f);
        }
    }
}
