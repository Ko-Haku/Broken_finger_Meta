using UnityEngine;
using Oculus.Interaction.Input;

// Artiglio steampunk: le due dita ruotano attorno all'asse del mozzo, una verso l'altra,
// in base alla distanza pollice-indice della mano tracciata (come PinchOpenDriver per la pinzetta).
public class TB_ArtiglioPinch : MonoBehaviour
{
    public Hand hand;
    public Transform ditoSup;               // pivot sul centro del mozzo
    public Transform ditoInf;
    public Transform mozzo;                 // per l'asse di rotazione

    [Header("Distanza pollice-indice (m)")]
    public float dChiuso = 0.015f;
    public float dAperto = 0.08f;
    [Tooltip("Rotazione di ciascun dito da aperto a chiuso (gradi)")]
    public float gradiChiusura = 21f;
    [Range(0, 1)] public float smoothing = 0.25f;

    [Header("Debug in editor senza visore")]
    public KeyCode tastoChiudi = KeyCode.Space;

    public float Apertura { get; private set; } = 1f;

    Quaternion supRiposo, infRiposo;
    Vector3 asseSupLocale, asseInfLocale;

    void Awake()
    {
        supRiposo = ditoSup.localRotation; infRiposo = ditoInf.localRotation;
        // asse del mozzo = direzione di minima estensione della sua mesh
        Vector3 asse = AsseMozzo();
        // verso: ruotando il dito superiore attorno all'asse deve andare verso l'inferiore
        Vector3 dSup = Direzione(ditoSup), dInf = Direzione(ditoInf);
        if (Vector3.Dot(Vector3.Cross(dSup, dInf), asse) < 0) asse = -asse;
        asseSupLocale = Quaternion.Inverse(ditoSup.rotation) * asse;
        asseInfLocale = Quaternion.Inverse(ditoInf.rotation) * asse;
    }

    Vector3 Direzione(Transform dito)
    {
        var r = dito.GetComponentsInChildren<Renderer>();
        Vector3 c = Vector3.zero; foreach (var x in r) c += x.bounds.center;
        return (c / Mathf.Max(1, r.Length) - dito.position).normalized;
    }

    Vector3 AsseMozzo()
    {
        var mf = mozzo.GetComponent<MeshFilter>();
        var b = mf ? mf.sharedMesh.bounds.size : Vector3.one;
        Vector3 l = b.x < b.y && b.x < b.z ? Vector3.right : (b.y < b.z ? Vector3.up : Vector3.forward);
        return mozzo.TransformDirection(l).normalized;
    }

    void Update()
    {
        float target = 1f;
        if (hand != null && hand.IsConnected
            && hand.GetJointPose(HandJointId.HandIndexTip, out var i)
            && hand.GetJointPose(HandJointId.HandThumbTip, out var t))
            target = Mathf.InverseLerp(dChiuso, dAperto, Vector3.Distance(i.position, t.position));
        if (Input.GetKey(tastoChiudi)) target = 0f;
        Apertura = Mathf.Lerp(Apertura, target, 1f - Mathf.Pow(1f - smoothing, Time.deltaTime * 90f));

        float g = (1f - Apertura) * gradiChiusura;
        ditoSup.localRotation = supRiposo * Quaternion.AngleAxis(g, asseSupLocale);
        ditoInf.localRotation = infRiposo * Quaternion.AngleAxis(-g, asseInfLocale);
    }
}
