using UnityEngine;

// Mantiene questo oggetto nella posa che aveva nel vecchio setup: ancora del tracking (es.
// RightHandAnchor) * offset fisso del vecchio target VRIK, con scala mondo fissa.
// Usato per la pinzetta: riproduce esattamente il montaggio del modello originale.
[DefaultExecutionOrder(32200)]
public class TB_SegueAncora : MonoBehaviour
{
    public Transform ancora;
    public Vector3 offsetPosizione;
    public Quaternion offsetRotazione = Quaternion.identity;
    [Tooltip("Scala mondo di questo oggetto (nel vecchio avatar l'osso della mano aveva scala 80)")]
    public float scalaMondo = 1f;

    void LateUpdate()
    {
        if (!ancora) return;
        transform.SetPositionAndRotation(ancora.TransformPoint(offsetPosizione), ancora.rotation * offsetRotazione);
        float p = transform.parent ? transform.parent.lossyScale.x : 1f;
        transform.localScale = Vector3.one * (scalaMondo / Mathf.Max(1e-6f, p));
    }
}
