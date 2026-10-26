using UnityEngine;
using Oculus.Interaction.Input;
using RootMotion.FinalIK;

// Calibrazione per soggetto, come nei giochi VR con corpo intero: in piedi, braccia a T, tasto K.
//  1) scala uniforme dell'avatar: altezza occhi avatar = altezza occhi del soggetto
//  2) lunghezza braccia: armLengthMlp di VRIK da apertura polso-polso del soggetto
// Le mani LM si scalano a parte (TB_ManoRealistica) sulle misure reali della mano.
public class TB_CalibrazioneCorpo : MonoBehaviour
{
    public VRIK[] avatar;
    public Transform testa;                 // CenterEyeAnchor
    public Transform pavimento;             // origine del tracking (floor level)
    public Hand manoSx;
    public Hand manoDx;
    public KeyCode tasto = KeyCode.K;

    [Header("Rilevamento automatico della T-pose")]
    public bool automatica = true;
    [Tooltip("Secondi di T-pose ferma prima di calibrare")]
    public float durataPosa = 2f;
    [Tooltip("Polsi sotto gli occhi di questa quantità (m): spalle")]
    public Vector2 sottoOcchi = new Vector2(0.10f, 0.35f);
    [Tooltip("Apertura polso-polso minima, in frazione dell'altezza degli occhi")]
    public float aperturaMinima = 0.75f;
    [Tooltip("Velocità massima dei polsi (m/s) per considerare la posa ferma")]
    public float velocitaMax = 0.15f;
    [Tooltip("Opzionale: testo di feedback (conto alla rovescia)")]
    public TMPro.TMP_Text feedback;

    float tPosa;
    Vector3 ultimoSx, ultimoDx;
    bool inPosaPrima;
    public bool Calibrato { get; private set; }

    [Header("Limiti di sicurezza")]
    public Vector2 limitiScala = new Vector2(0.7f, 1.35f);
    public Vector2 limitiBraccio = new Vector2(0.8f, 1.25f);

    class Bind { public float scala0, occhi0, apertura0, braccio0; }
    Bind[] bind;

    void Awake()
    {
        // misure dell'avatar in posa di bind (T-pose), prima che VRIK lo muova
        bind = new Bind[avatar.Length];
        for (int i = 0; i < avatar.Length; i++)
        {
            var ik = avatar[i]; var r = ik.references; var a = ik.GetComponent<Animator>();
            Transform occhio = a ? a.GetBoneTransform(HumanBodyBones.LeftEye) : null;
            float yOcchi = occhio ? occhio.position.y : r.head.position.y + 0.08f * ik.transform.lossyScale.y;
            bind[i] = new Bind
            {
                scala0 = ik.transform.localScale.x,
                occhi0 = yOcchi - ik.transform.position.y,
                apertura0 = Vector3.Distance(r.leftHand.position, r.rightHand.position),
                braccio0 = Vector3.Distance(r.rightUpperArm.position, r.rightForearm.position)
                         + Vector3.Distance(r.rightForearm.position, r.rightHand.position),
            };
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(tasto)) Calibra();
        if (automatica) ControllaPosa();
    }

    // Calibra da sola dopo 'durataPosa' secondi di T-pose ferma; per ricalibrare basta
    // uscire dalla posa e rifarla.
    void ControllaPosa()
    {
        bool inPosa = false;
        if (Polso(manoSx, out var ps) && Polso(manoDx, out var pd))
        {
            float occhi = testa.position.y - pavimento.position.y;
            float y = testa.position.y;
            bool spalle = InRange(y - ps.y, sottoOcchi) && InRange(y - pd.y, sottoOcchi);
            bool aperte = Vector3.Distance(ps, pd) > aperturaMinima * occhi;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            bool ferme = inPosaPrima && (ps - ultimoSx).magnitude / dt < velocitaMax && (pd - ultimoDx).magnitude / dt < velocitaMax;
            inPosa = spalle && aperte;
            ultimoSx = ps; ultimoDx = pd;
            if (inPosa && (ferme || tPosa == 0f)) tPosa += Time.deltaTime;
            else if (!inPosa) tPosa = 0f;
        }
        else tPosa = 0f;

        if (!inPosa && inPosaPrima && feedback) feedback.text = "";
        inPosaPrima = inPosa;

        if (inPosa && tPosa > 0f)
        {
            if (tPosa >= durataPosa)
            {
                if (Calibra() && feedback) feedback.text = "Calibrato!";
                tPosa = -999f;   // non ripetere finché non si esce dalla posa
            }
            else if (feedback) feedback.text = $"Resta a T... {Mathf.CeilToInt(durataPosa - tPosa)}";
        }
    }

    static bool InRange(float v, Vector2 r) => v >= r.x && v <= r.y;

    public bool Calibra()
    {
        if (!Polso(manoSx, out var ps) || !Polso(manoDx, out var pd))
        {
            Debug.LogWarning("[TB] Calibrazione corpo: servono entrambe le mani tracciate (braccia a T)");
            return false;
        }
        float occhiUtente = testa.position.y - pavimento.position.y;
        float aperturaUtente = Vector3.Distance(ps, pd);

        for (int i = 0; i < avatar.Length; i++)
        {
            var ik = avatar[i]; var b = bind[i];
            float k = Mathf.Clamp(occhiUtente / b.occhi0, limitiScala.x, limitiScala.y);
            ik.transform.localScale = Vector3.one * b.scala0 * k;

            // apertura dell'avatar a questa scala; la differenza va divisa sulle due braccia
            float aperturaAvatar = b.apertura0 * k;
            float braccioAvatar = b.braccio0 * k;
            float mlp = Mathf.Clamp(1f + (aperturaUtente - aperturaAvatar) / (2f * braccioAvatar), limitiBraccio.x, limitiBraccio.y);
            ik.solver.leftArm.armLengthMlp = mlp;
            ik.solver.rightArm.armLengthMlp = mlp;
            Calibrato = true;
            Debug.Log($"[TB] {ik.name}: occhi soggetto {occhiUtente:F2} m, scala {k:F2}; apertura soggetto {aperturaUtente:F2} m, avatar {aperturaAvatar:F2} m, braccia x{mlp:F2}");
        }
        return true;
    }

    static bool Polso(Hand h, out Vector3 p)
    {
        p = default;
        if (h == null || !h.IsConnected || !h.IsTrackedDataValid) return false;
        if (!h.GetJointPose(HandJointId.HandWristRoot, out var pose)) return false;
        p = pose.position;
        return true;
    }
}
