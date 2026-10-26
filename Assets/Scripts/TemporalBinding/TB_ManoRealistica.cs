using UnityEngine;
using Oculus.Interaction.Input;

// Pilota le mani realistiche LM (rig Leap: polso + 3 falangi per dito, asse +Y lungo l'osso)
// con l'hand tracking Meta, senza modificare il modello in Blender.
// Metodo: a una posa di calibrazione (mano aperta) si costruisce per entrambi i rig un sistema
// anatomico dalle sole posizioni (polso->nocca medio = avanti, indice-mignolo = laterale),
// e per ogni osso un offset che allinea la falange LM alla falange tracciata.
// A runtime: rotazione LM = rotazione Meta * offset (+ piccola correzione di direzione).
// Gira dopo FinalIK (VRIK), che nel modello avatar posiziona braccio e polso.
[DefaultExecutionOrder(32000)]
public class TB_ManoRealistica : MonoBehaviour
{
    public Hand hand;
    [Tooltip("Usata quando 'hand' non è tracciata, es. mano guidata dal controller (OVRControllerDrivenHands)")]
    public Hand handAlternativa;
    Hand corrente;
    [Tooltip("Radice del modello LM (verrà spostata e scalata)")]
    public Transform radiceModello;
    [Tooltip("Mano innestata in un avatar: VRIK posiziona il braccio, qui si ruotano solo polso e dita e si scala solo la mano")]
    public bool modalitaAvatar;
    [Tooltip("Target della mano di VRIK: messo a ogni frame sul polso tracciato con la rotazione del polso LM, così VRIK calcola gomito e torsione con l'orientamento giusto")]
    public Transform targetIK;
    public Transform polso;

    [Header("Ossa LM: A, B, C per dito")]
    public Transform[] pollice = new Transform[3];
    public Transform[] indice = new Transform[3];
    public Transform[] medio = new Transform[3];
    public Transform[] anulare = new Transform[3];
    public Transform[] mignolo = new Transform[3];

    [Header("Calibrazione")]
    [Tooltip("Scala il modello sulla lunghezza del palmo dell'utente")]
    public bool adattaScala = true;
    [Tooltip("Angolo massimo tra falangi per considerare la mano 'aperta' e calibrare (gradi)")]
    public float maxCurlCalibrazione = 20f;
    public KeyCode tastoRicalibra = KeyCode.C;
    [Tooltip("Lunghezza dell'ultima falange (m, prima della scala) per creare le punte")]
    public float lunghezzaPunta = 0.022f;
    public Renderer[] renderers;

    public Transform PuntaPollice { get; private set; }
    public Transform PuntaIndice { get; private set; }
    public bool Calibrata { get; private set; }

    static readonly HandJointId[][] JointMeta =
    {
        new[] { HandJointId.HandThumb1, HandJointId.HandThumb2, HandJointId.HandThumb3, HandJointId.HandThumbTip },
        new[] { HandJointId.HandIndex1, HandJointId.HandIndex2, HandJointId.HandIndex3, HandJointId.HandIndexTip },
        new[] { HandJointId.HandMiddle1, HandJointId.HandMiddle2, HandJointId.HandMiddle3, HandJointId.HandMiddleTip },
        new[] { HandJointId.HandRing1, HandJointId.HandRing2, HandJointId.HandRing3, HandJointId.HandRingTip },
        new[] { HandJointId.HandPinky1, HandJointId.HandPinky2, HandJointId.HandPinky3, HandJointId.HandPinkyTip },
    };

    Transform[][] ossa;
    Quaternion[][] offset;
    Quaternion offsetPolso;
    Quaternion[][] bindInPolso;       // rotazioni LM di bind nello spazio del polso LM
    Vector3[][] bindPosInPolso;
    Quaternion frameLM;               // sistema anatomico LM nello spazio del polso LM
    Vector3 scalaIniziale;
    float scalaMondo = -1f;           // scala mondo della mano = mano reale del soggetto

    // La mano resta della grandezza reale anche se la calibrazione del corpo scala l'avatar.
    void ApplicaScalaMondo()
    {
        float p = Scalato.parent ? Scalato.parent.lossyScale.x : 1f;
        Scalato.localScale = scalaIniziale * (scalaMondo / Mathf.Max(1e-6f, p * scalaIniziale.x));
    }

    void Awake()
    {
        ossa = new[] { pollice, indice, medio, anulare, mignolo };
        scalaIniziale = Scalato.localScale;

        bindInPolso = new Quaternion[5][];
        bindPosInPolso = new Vector3[5][];
        for (int f = 0; f < 5; f++)
        {
            bindInPolso[f] = new Quaternion[3];
            bindPosInPolso[f] = new Vector3[3];
            for (int b = 0; b < 3; b++)
            {
                bindInPolso[f][b] = Quaternion.Inverse(polso.rotation) * ossa[f][b].rotation;
                bindPosInPolso[f][b] = polso.InverseTransformPoint(ossa[f][b].position);
            }
        }
        // sistema anatomico LM dalle posizioni di bind (spazio del polso LM, in unità locali)
        frameLM = Frame(Vector3.zero, bindPosInPolso[2][0], bindPosInPolso[1][0], bindPosInPolso[4][0]);

        PuntaPollice = CreaPunta(pollice[2], "PuntaPollice");
        PuntaIndice = CreaPunta(indice[2], "PuntaIndice");
        Mostra(false);
    }

    // In modalità avatar si scala solo la mano (polso), altrimenti tutto il modello.
    Transform Scalato => modalitaAvatar ? polso : radiceModello;

    Transform CreaPunta(Transform falange, string nome)
    {
        var t = new GameObject(nome).transform;
        t.SetParent(falange, false);
        // +Y locale = direzione dell'osso
        t.position = falange.position + falange.up * lunghezzaPunta;
        return t;
    }

    static Quaternion Frame(Vector3 polso, Vector3 medio1, Vector3 indice1, Vector3 mignolo1)
    {
        Vector3 avanti = (medio1 - polso).normalized;
        Vector3 laterale = (indice1 - mignolo1).normalized;
        Vector3 su = Vector3.Cross(avanti, laterale).normalized;
        return Quaternion.LookRotation(avanti, su);
    }

    void Mostra(bool on)
    {
        if (renderers == null) return;
        foreach (var r in renderers) if (r) r.enabled = on;
    }

    bool Pose(HandJointId id, out Pose p) => corrente.GetJointPose(id, out p);

    static bool Valida(Hand h) => h != null && h.isActiveAndEnabled && h.IsConnected && h.IsTrackedDataValid;

    // Calibrazione condivisa tra avatar che usano la stessa mano tracciata (stesso scheletro):
    // passando da Avatar_Mano ad Avatar_Tool non serve ricalibrare.
    class DatiCalibrazione { public Quaternion polso; public Quaternion[][] dita; public float scala; }
    static readonly System.Collections.Generic.Dictionary<Hand, DatiCalibrazione> condivisa =
        new System.Collections.Generic.Dictionary<Hand, DatiCalibrazione>();

    bool DaCondivisa()
    {
        if (hand == null || !condivisa.TryGetValue(hand, out var d)) return false;
        if (adattaScala && d.scala <= 0f) return false;   // scala mai misurata: calibra da sé
        offsetPolso = d.polso; offset = d.dita;
        if (adattaScala) { scalaMondo = d.scala; ApplicaScalaMondo(); }
        Calibrata = true;
        Debug.Log($"[TB] {name}/{polso.name}: calibrazione condivisa applicata");
        return true;
    }

    // per la diagnostica
    public string Sorgente => corrente == null ? "nessuna" : corrente == hand ? "tracking" : "controller";
    public bool AltaConfidenza => corrente != null && corrente.IsHighConfidence;

    void ScegliSorgente() => corrente = Valida(hand) ? hand : Valida(handAlternativa) ? handAlternativa : null;

    // Prima del solve di VRIK (LateUpdate): aggiorna il target della mano.
    void Update()
    {
        if (targetIK == null || !Calibrata) return;
        ScegliSorgente();
        if (corrente != null && Pose(HandJointId.HandWristRoot, out var w))
            targetIK.SetPositionAndRotation(w.position, w.rotation * offsetPolso);
    }

    void LateUpdate()
    {
        ScegliSorgente();
        if (corrente == null) { Mostra(false); return; }

        if (Input.GetKeyDown(tastoRicalibra)) { Calibrata = false; condivisa.Remove(hand); }
        if (!Calibrata && !DaCondivisa() && !ProvaCalibrazione()) { Mostra(false); return; }
        Mostra(true);
        if (adattaScala && scalaMondo > 0f) ApplicaScalaMondo();

        Pose w;
        if (!Pose(HandJointId.HandWristRoot, out w)) return;

        // polso: posizione e rotazione
        polso.rotation = w.rotation * offsetPolso;
        if (!modalitaAvatar) radiceModello.position += w.position - polso.position;

        // dita: dalla base alla punta (gerarchia padre->figlio)
        for (int f = 0; f < 5; f++)
        {
            for (int b = 0; b < 3; b++)
            {
                if (!Pose(JointMeta[f][b], out var pj) || !Pose(JointMeta[f][b + 1], out var pn)) continue;
                Quaternion r = pj.rotation * offset[f][b];
                // correzione: l'asse dell'osso LM deve puntare lungo la falange tracciata
                Vector3 dir = pn.position - pj.position;
                if (dir.sqrMagnitude > 1e-8f) r = Quaternion.FromToRotation(r * Vector3.up, dir) * r;
                ossa[f][b].rotation = r;
            }
        }
    }

    bool ProvaCalibrazione()
    {
        // serve una mano aperta e ben tracciata
        // si calibra solo sulla mano tracciata vera, aperta e ben vista
        if (corrente != hand || !hand.IsHighConfidence) return false;
        var pos = new Vector3[5][];
        var rot = new Quaternion[5][];
        for (int f = 0; f < 5; f++)
        {
            pos[f] = new Vector3[4];
            rot[f] = new Quaternion[4];
            for (int j = 0; j < 4; j++)
            {
                if (!Pose(JointMeta[f][j], out var p)) return false;
                pos[f][j] = p.position; rot[f][j] = p.rotation;
            }
            if (f > 0)
                for (int j = 0; j < 2; j++)
                    if (Vector3.Angle(pos[f][j + 1] - pos[f][j], pos[f][j + 2] - pos[f][j + 1]) > maxCurlCalibrazione) return false;
        }
        if (!Pose(HandJointId.HandWristRoot, out var w)) return false;

        // scala: larghezza del palmo alle nocche e lunghezza del dito medio, che corrispondono
        // anatomicamente nei due rig (il "polso" Meta sta più indietro di quello LM)
        float palmoUtente = Vector3.Distance(pos[1][0], pos[4][0]);
        float ditoUtente = Vector3.Distance(pos[2][0], pos[2][1]) + Vector3.Distance(pos[2][1], pos[2][2]);
        if (adattaScala)
        {
            Scalato.localScale = scalaIniziale;
            float palmoModello = Vector3.Distance(indice[0].position, mignolo[0].position);
            float ditoModello = Vector3.Distance(medio[0].position, medio[1].position) + Vector3.Distance(medio[1].position, medio[2].position);
            float k = 0.5f * (palmoUtente / Mathf.Max(1e-6f, palmoModello) + ditoUtente / Mathf.Max(1e-6f, ditoModello));
            Scalato.localScale = scalaIniziale * k;
            scalaMondo = Scalato.lossyScale.x;
        }

        // sistema anatomico tracciato (mondo) e posa LM corrispondente
        Quaternion frameUtente = Frame(w.position, pos[2][0], pos[1][0], pos[4][0]);
        Quaternion polsoLM = frameUtente * Quaternion.Inverse(frameLM);   // rotazione mondo del polso LM
        offsetPolso = Quaternion.Inverse(w.rotation) * polsoLM;

        offset = new Quaternion[5][];
        for (int f = 0; f < 5; f++)
        {
            offset[f] = new Quaternion[3];
            for (int b = 0; b < 3; b++)
            {
                Quaternion L = polsoLM * bindInPolso[f][b];
                Quaternion D = Quaternion.FromToRotation(L * Vector3.up, pos[f][b + 1] - pos[f][b]) * L;
                offset[f][b] = Quaternion.Inverse(rot[f][b]) * D;
            }
        }
        Calibrata = true;
        if (hand != null)
            condivisa[hand] = new DatiCalibrazione
            {
                polso = offsetPolso, dita = offset,
                // chi non scala la mano (moncherino) conserva la scala misurata da un'altra
                scala = adattaScala ? scalaMondo
                      : condivisa.TryGetValue(hand, out var prec) ? prec.scala : -1f,
            };
        Debug.Log($"[TB] {name}/{polso.name} calibrata: palmo {palmoUtente * 100f:F1} cm, dito medio {ditoUtente * 100f:F1} cm, scala mano {Scalato.localScale.x / scalaIniziale.x:F2}");
        return true;
    }
}
