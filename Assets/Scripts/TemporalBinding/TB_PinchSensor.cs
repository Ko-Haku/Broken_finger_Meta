using UnityEngine;
using Oculus.Interaction.Input;

// Legge l'apertura pollice-indice dalla mano tracciata e rileva la chiusura sul clicker.
// La stessa azione (pinch) pilota sia la mano sia la pinzetta, così tra Mano e Tool
// cambia solo l'effettore visto, non il movimento.
public class TB_PinchSensor : MonoBehaviour
{
    public Hand hand;
    public Transform clicker;               // centro del pulsante da stringere

    [Header("Calibrazione (metri)")]
    public float dMin = 0.015f;             // dita a contatto
    public float dMax = 0.08f;              // mano ben aperta

    [Header("Soglie (apertura normalizzata 0-1)")]
    [Tooltip("Sotto questa soglia la presa è chiusa = evento azione")]
    public float sogliaChiusura = 0.2f;
    [Tooltip("Con la pinzetta: le punte si chiudono già a dita reali ~3 cm (PinchOpenDriver dMin), quindi la soglia è più alta")]
    public float sogliaChiusuraTool = 0.3f;
    [Tooltip("Sopra questa soglia la mano è considerata pronta (aperta)")]
    public float sogliaApertura = 0.6f;
    [Tooltip("Distanza massima del punto medio pollice-indice dal clicker")]
    public float raggioClicker = 0.05f;
    [Tooltip("Apertura (0-1) a cui le dita toccano il pulsante: da qui a sogliaChiusura il tasto si abbassa con le dita")]
    public float aperturaContatto = 0.45f;

    [Header("Punto di presa visibile (quello con cui il partecipante mira)")]
    [Tooltip("Punte di pollice e indice dell'avatar Mano: la mano dell'avatar è scalata, le sue dita non coincidono con quelle reali")]
    public Transform puntaManoA;
    public Transform puntaManoB;
    [Tooltip("Tool: la presa è fatta dalle punte della pinzetta, non dalle dita")]
    public Transform puntaPinzaA;
    public Transform puntaPinzaB;

    [Header("Pinzetta sul pulsante (metri)")]
    [Tooltip("Piattello + tasto: lo spazio che le punte devono stringere")]
    public float spessorePulsante = 0.014f;
    public float corsaTasto = 0.005f;
    [Tooltip("Distanza tra i punti di riferimento delle punte a pinzetta chiusa (la misura TB_PinzaSulTasto)")]
    public float gapChiusoTool = 0.007f;
    // punte appena a contatto con il pulsante / a tasto completamente premuto
    public float GapContattoTool => spessorePulsante + gapChiusoTool;
    public float GapMinimoTool => GapContattoTool - corsaTasto;

    // le punte sono figlie dei rispettivi avatar: valgono solo quando quell'avatar è attivo
    static bool Attive(Transform a, Transform b) => a && b && a.gameObject.activeInHierarchy;

    [Header("Debug in editor senza visore")]
    public KeyCode tastoSimula = KeyCode.Space;

    public float Apertura { get; private set; } = 1f;
    public bool Tracciata { get; private set; }
    public Vector3 PuntoPresa { get; private set; }
    public Vector3 PosizionePolso { get; private set; }

    public bool Pronta => Apertura > sogliaApertura && VicinoAlClicker;

    [Header("Passivo: mano a riposo sul pulsante")]
    [Tooltip("Distanza massima del punto di presa dal tasto (più larga che nell'attivo: la mano è rilassata)")]
    public float raggioPassivo = 0.08f;
    [Tooltip("Apertura minima: le dita non devono stringere (sopra la soglia di contatto)")]
    public float aperturaPassivo = 0.35f;
    public bool ManoAlTasto => clicker == null || Vector3.Distance(PuntoPresa, clicker.position) < raggioPassivo;
    public bool ProntaPassivo => Tracciata && ManoAlTasto && Apertura > aperturaPassivo;
    public bool VicinoAlClicker => clicker == null || Vector3.Distance(PuntoPresa, clicker.position) < raggioClicker;

    // 0 = tasto a riposo, 1 = tasto a fondo corsa: segue la chiusura delle dita attorno al pulsante
    // Mano: dall'apertura delle dita reali. Tool: dalla distanza vera tra le punte visibili della pinzetta,
    // così il tasto comincia a scendere appena le punte lo toccano.
    public float Compressione
    {
        get
        {
            if (!(Tracciata || simulata) || !VicinoAlClicker) return 0f;
            if (ConTool)
                return Mathf.Clamp01(Mathf.InverseLerp(GapContattoTool, GapMinimoTool, Vector3.Distance(puntaPinzaA.position, puntaPinzaB.position)));
            return Mathf.Clamp01(Mathf.InverseLerp(aperturaContatto, SogliaAttiva, Apertura));
        }
    }

    public bool ConTool => Attive(puntaPinzaA, puntaPinzaB);
    public float SogliaAttiva => ConTool ? sogliaChiusuraTool : sogliaChiusura;

    bool chiusa;
    bool simulata;

    // Dita congelate dal tracking: il polso si muove ma pollice e indice restano identici rispetto al polso
    // (il tracciamento Meta a volte "incolla" le dita con confidenza ancora alta, tipicamente in pinch occluso).
    [Header("Diagnostica")]
    public bool segnalaDitaCongelate = true;
    public bool DitaCongelate { get; private set; }
    public int EpisodiCongelamento { get; private set; }
    Vector3 relIdx, relTh, polsoRif;
    float tFermo;

    void ControllaCongelamento(Vector3 idx, Vector3 th)
    {
        if (!segnalaDitaCongelate || !hand.GetJointPose(HandJointId.HandWristRoot, out var w)) return;
        var inv = Quaternion.Inverse(w.rotation);
        Vector3 ri = inv * (idx - w.position), rt = inv * (th - w.position);
        bool ditaFerme = (ri - relIdx).magnitude < 0.0005f && (rt - relTh).magnitude < 0.0005f;
        relIdx = ri; relTh = rt;
        if (!ditaFerme) { tFermo = 0f; polsoRif = w.position; if (DitaCongelate) { DitaCongelate = false; Debug.Log("[TB] Dita di nuovo seguite dal tracking"); } return; }
        tFermo += Time.unscaledDeltaTime;
        // dita immobili al decimo di millimetro per 1 s mentre il polso si è spostato di 3 cm: non è una mano ferma
        if (!DitaCongelate && tFermo > 1f && Vector3.Distance(w.position, polsoRif) > 0.03f)
        {
            DitaCongelate = true; EpisodiCongelamento++;
            Debug.LogWarning($"[TB] Dita congelate dal tracking (episodio {EpisodiCongelamento}, t={Time.realtimeSinceStartup:F1} s): " +
                             $"apertura ferma a {Apertura:F2}, punto di presa a {(clicker ? Vector3.Distance(PuntoPresa, clicker.position) * 100f : 0f):F1} cm dal tasto");
        }
    }

    // true solo nel frame in cui la presa si chiude vicino al clicker
    public bool ChiusuraQuestoFrame { get; private set; }

    void Update()
    {
        ChiusuraQuestoFrame = false;

        if (Input.GetKey(tastoSimula)) { simulata = true; }

        Pose idx = default, th = default;
        Tracciata = hand != null && hand.IsConnected
            && hand.GetJointPose(HandJointId.HandIndexTip, out idx)
            && hand.GetJointPose(HandJointId.HandThumbTip, out th);

        if (Tracciata)
        {
            if (hand.GetJointPose(HandJointId.HandWristRoot, out var wr)) PosizionePolso = wr.position;
            PuntoPresa = Attive(puntaPinzaA, puntaPinzaB) ? (puntaPinzaA.position + puntaPinzaB.position) * 0.5f
                       : Attive(puntaManoA, puntaManoB) ? (puntaManoA.position + puntaManoB.position) * 0.5f
                       : (idx.position + th.position) * 0.5f;
            Apertura = Mathf.InverseLerp(dMin, dMax, Vector3.Distance(idx.position, th.position));
        }
        else if (simulata)
        {
            // in editor: tieni premuto Spazio per "chiudere"
            Apertura = Input.GetKey(tastoSimula) ? 0f : 1f;
            if (clicker) PuntoPresa = clicker.position;
        }

        if (Tracciata) ControllaCongelamento(idx.position, th.position);

        if (!chiusa && Apertura < SogliaAttiva && VicinoAlClicker)
        {
            chiusa = true;
            ChiusuraQuestoFrame = true;
        }
        else if (chiusa && Apertura > sogliaApertura)
        {
            chiusa = false;
        }
    }
}
