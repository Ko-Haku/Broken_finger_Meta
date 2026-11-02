using UnityEngine;
using Oculus.Interaction.Input;

// Allinea il piano del tavolo virtuale a quello reale: il partecipante appoggia la mano destra
// piatta sul tavolo vero e lo sperimentatore preme V. Tutta la postazione (tavolo, lampadina,
// pulsante su asta, pistone) sale o scende in blocco, insieme al mirino.
// L'altezza resta salvata (PlayerPrefs) e viene riapplicata all'avvio: il tavolo reale è sempre lo stesso.
// Da fare prima della calibrazione del pulsante (C).
[DefaultExecutionOrder(-100)]
public class TB_CalibraTavolo : MonoBehaviour
{
    public Hand hand;                        // mano destra tracciata (la stessa del sensore)
    public Transform pianoTavolo;            // oggetto Tavolo: se ne usa il bordo superiore
    public Transform[] seguono;              // oggetti fuori dalla postazione da spostare insieme (mirino)
    [Tooltip("Mezzo spessore del polpastrello: le punte tracciate stanno sopra la superficie")]
    public float spessoreDita = 0.008f;
    public KeyCode tasto = KeyCode.V;
    public bool ricordaAltezza = true;

    const string Chiave = "TB_AltezzaTavolo";

    float TopTavolo => pianoTavolo ? pianoTavolo.GetComponentInChildren<Renderer>().bounds.max.y : transform.position.y;

    void Start()
    {
        if (ricordaAltezza && PlayerPrefs.HasKey(Chiave))
        {
            Sposta(PlayerPrefs.GetFloat(Chiave) - TopTavolo);
            Debug.Log($"[TB] Tavolo virtuale all'altezza salvata: {TopTavolo * 100f:F1} cm (V per ricalibrare)");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(tasto)) Calibra();
    }

    [ContextMenu("Calibra sulla mano appoggiata")]
    public void Calibra()
    {
        if (hand == null || !hand.IsConnected) { Debug.LogWarning("[TB] Calibrazione tavolo: mano non tracciata"); return; }
        float minY = float.MaxValue; int n = 0;
        foreach (var j in new[] { HandJointId.HandIndexTip, HandJointId.HandMiddleTip, HandJointId.HandRingTip, HandJointId.HandPinkyTip, HandJointId.HandThumbTip })
            if (hand.GetJointPose(j, out var p)) { minY = Mathf.Min(minY, p.position.y); n++; }
        if (n == 0) { Debug.LogWarning("[TB] Calibrazione tavolo: punte delle dita non disponibili"); return; }

        float piano = minY - spessoreDita;
        float delta = piano - TopTavolo;
        Sposta(delta);
        if (ricordaAltezza) { PlayerPrefs.SetFloat(Chiave, piano); PlayerPrefs.Save(); }
        Debug.Log($"[TB] Tavolo calibrato: piano a {piano * 100f:F1} cm (spostato di {delta * 100f:+0.0;-0.0} cm). Ora calibra il pulsante (C)");
    }

    void Sposta(float delta)
    {
        if (Mathf.Abs(delta) < 1e-4f) return;
        transform.position += Vector3.up * delta;
        if (seguono != null) foreach (var t in seguono) if (t) t.position += Vector3.up * delta;
    }

    [ContextMenu("Dimentica altezza salvata")]
    void Dimentica() { PlayerPrefs.DeleteKey(Chiave); Debug.Log("[TB] Altezza del tavolo salvata cancellata"); }
}
