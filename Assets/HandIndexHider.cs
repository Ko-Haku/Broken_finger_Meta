using UnityEngine;

public class HandIndexHider : MonoBehaviour
{
    [Tooltip("Il nodo XR dell'indice da nascondere/mostrare (es. XRHand_IndexMetacarpal)")]
    public Transform indexMetacarpal;

    private Vector3 hiddenScale = Vector3.zero;
    private Vector3 visibleScale = Vector3.one;

    public void SetVisible(bool visible)
    {
        if (indexMetacarpal == null) return;
        indexMetacarpal.localScale = visible ? visibleScale : hiddenScale;
    }
}