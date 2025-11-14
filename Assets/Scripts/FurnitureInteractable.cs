using UnityEngine;

[DisallowMultipleComponent]
public class FurnitureInteractable : MonoBehaviour
{
    [Tooltip("Identificador lógico para persistencia")]
    public string furnitureId;

    [Tooltip("Capa de raycast (poner en 'Furniture')")]
    public LayerMask selectableLayer;

    public void SetHighlight(bool on)
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            r.material.SetFloat("_Selected", on ? 1f : 0f);
        }
    }
}
