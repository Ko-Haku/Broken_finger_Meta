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
    [Tooltip("Sopra questa soglia la mano è considerata pronta (aperta)")]
    public float sogliaApertura = 0.6f;
    [Tooltip("Distanza massima del punto medio pollice-indice dal clicker")]
    public float raggioClicker = 0.05f;

    [Header("Debug in editor senza visore")]
    public KeyCode tastoSimula = KeyCode.Space;

    public float Apertura { get; private set; } = 1f;
    public bool Tracciata { get; private set; }
    public Vector3 PuntoPresa { get; private set; }
    public Vector3 PosizionePolso { get; private set; }

    public bool Pronta => Apertura > sogliaApertura && VicinoAlClicker;
    public bool VicinoAlClicker => clicker == null || Vector3.Distance(PuntoPresa, clicker.position) < raggioClicker;

    bool chiusa;
    bool simulata;

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
            PuntoPresa = (idx.position + th.position) * 0.5f;
            Apertura = Mathf.InverseLerp(dMin, dMax, Vector3.Distance(idx.position, th.position));
        }
        else if (simulata)
        {
            // in editor: tieni premuto Spazio per "chiudere"
            Apertura = Input.GetKey(tastoSimula) ? 0f : 1f;
            if (clicker) PuntoPresa = clicker.position;
        }

        if (!chiusa && Apertura < sogliaChiusura && VicinoAlClicker)
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
