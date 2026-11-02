using UnityEngine;
using Oculus.Interaction.Input;

// Apre e chiude un tool animato (tentacolo, gripper a due ganasce del progetto Eseguito/Osservato)
// con la distanza tra pollice e indice della mano tracciata: come GripperBlendController di quel
// progetto, ma senza la logica del bicchiere. 0 = chiuso (dita a 'chiusoCm'), 1 = aperto (a 'apertoCm').
// Il controller dell'Animator fa una blend tra la clip all'indietro (0) e in avanti (1).
public class TB_AperturaTool : MonoBehaviour
{
    public Hand hand;
    public Animator animator;
    [Tooltip("Parametro float dell'Animator: 'open' per il tentacolo, 'Blend' per il gripper")]
    public string parametro = "open";
    public float chiusoCm = 3f;
    public float apertoCm = 10f;
    [Tooltip("Secondi di smorzamento (0 = nessuno)")]
    public float smorzamento = 0.03f;
    [Header("Senza visore: tieni premuto Spazio per chiudere")]
    public KeyCode tastoChiudi = KeyCode.Space;

    public float Apertura { get; private set; } = 1f;
    [Tooltip(">= 0: apertura imposta da fuori (presa del bicchiere negli stimoli), al posto di quella delle dita")]
    [System.NonSerialized] public float aperturaForzata = -1f;
    float vel;
    int hashParam;

    void Awake() { hashParam = Animator.StringToHash(parametro); }

    void Update()
    {
        if (!animator) return;
        float target = Apertura;
        if (hand != null && hand.IsConnected &&
            hand.GetJointPose(HandJointId.HandIndexTip, out var idx) && hand.GetJointPose(HandJointId.HandThumbTip, out var th))
            target = Mathf.InverseLerp(chiusoCm, apertoCm, Vector3.Distance(idx.position, th.position) * 100f);
        if (Input.GetKey(tastoChiudi)) target = 0f;
        if (aperturaForzata >= 0f) target = aperturaForzata;
        Apertura = smorzamento > 0f ? Mathf.SmoothDamp(Apertura, target, ref vel, smorzamento) : target;
        animator.SetFloat(hashParam, Apertura);
    }
}
