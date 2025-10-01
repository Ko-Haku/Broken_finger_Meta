using UnityEngine;

public class GripperController : MonoBehaviour
{
    [Header("Input distanza (punte dita)")]
    public Transform puntoA;                // es. punta pollice
    public Transform puntoB;                // es. punta indice

    [Header("Ganascia che si muove")]
    public Transform topJaw;                // SOLO questa ruota
    public Vector3 hingeAxisLocal = Vector3.right; // asse locale della cerniera (X/Y/Z locali)

    [Header("Mappatura distanza -> angolo")]
    [Tooltip("Distanza (cm) considerata 'chiuso/neutral'")]
    public float neutralDistanceCm = 3f;
    [Tooltip("Distanza (cm) per apertura massima")]
    public float openDistanceCm = 7f;

    [SerializeField, Tooltip("Angolo massimo (deg) 'rigido' (90/120/150)")]
    private float maxAngleDeg = 90f; // baseline

    /// Setter/Getter usato da ExperimentRT (90/120/150).
    public float MaxAngleDeg
    {
        get => maxAngleDeg;
        set => maxAngleDeg = value;
    }

    [Header("Soft limit (gioco oltre il max)")]
    [Tooltip("Gradi extra 'elastici' oltre il max rigido (gioco).")]
    public float softExtraDeg = 12f;
    [Tooltip("Isteresi in rientro (opz) per non schioccare subito sotto il max).")]
    public float hysteresisDeg = 2f;

    [Header("Direzione / segno")]
    [Tooltip("Se l'angolo va nella direzione opposta, inverti")]
    public bool invertAngle = false;

    [Header("Smoothing")]
    [Tooltip("Tempo di smussamento (s) verso il nuovo angolo")]
    public float smoothTime = 0.06f;

    // runtime
    private Quaternion topJawInitialLocalRot;
    private float currentAngle;
    private float angleVel;

    void Awake()
    {
        if (topJaw == null)
        {
            Debug.LogWarning($"{name}/GripperController: topJaw non assegnata.");
            enabled = false;
            return;
        }
        topJawInitialLocalRot = topJaw.localRotation;
    }

    void Update()
    {
        if (puntoA == null || puntoB == null || topJaw == null) return;

        // distanza in cm
        float distCm = Vector3.Distance(puntoA.position, puntoB.position) * 100f;

        // normalizza 0..1 tra neutral e open (0=chiuso, 1=aperto)
        float t = Mathf.InverseLerp(neutralDistanceCm, openDistanceCm, distCm);
        t = Mathf.Clamp01(t);

        // 1) target "grezzo" che POTREBBE arrivare fino al soft-max
        float softMax = Mathf.Abs(maxAngleDeg) + Mathf.Max(0f, softExtraDeg);
        float targetRaw = t * softMax;

        // 2) soft-knee: sotto max -> lineare; sopra max -> curva morbida fino al softMax
        float desired;
        if (targetRaw <= maxAngleDeg)
        {
            desired = targetRaw;
        }
        else
        {
            // porzione oltre il max, normalizzata 0..1
            float over = targetRaw - maxAngleDeg;
            float u = Mathf.Clamp01(softExtraDeg <= 0f ? 0f : (over / softExtraDeg));

            // smoothstep: 0 -> 0, 1 -> 1, con derivata 0 agli estremi (curva morbida)
            float eased = u * u * (3f - 2f * u);

            desired = maxAngleDeg + eased * softExtraDeg;
        }

        // Isteresi in rientro (opzionale): se stavi sopra al max, non rientrare “a scatto”
        if (currentAngle > maxAngleDeg && desired < maxAngleDeg)
        {
            desired = Mathf.Max(desired, maxAngleDeg - Mathf.Max(0f, hysteresisDeg));
        }

        if (invertAngle) desired = -desired;

        // clamp di sicurezza (± 2× softMax)
        float hardClamp = softMax * 2f;
        desired = Mathf.Clamp(desired, -hardClamp, hardClamp);

        // smoothing verso il nuovo target
        currentAngle = Mathf.SmoothDamp(currentAngle, desired, ref angleVel, smoothTime);

        // applica rotazione solo alla ganascia superiore attorno all'asse locale definito
        var q = Quaternion.AngleAxis(currentAngle, hingeAxisLocal.normalized);
        topJaw.localRotation = topJawInitialLocalRot * q;
    }

    // --- API coerente col nuovo paradigma: Baseline / Stretch1 / Stretch2 ---
    public void SetStretch(ExperimentRT.Stretch s)
    {
        switch (s)
        {
            case ExperimentRT.Stretch.Baseline:
                MaxAngleDeg = 90f;   // baseline
                break;
            case ExperimentRT.Stretch.Stretch1:
                MaxAngleDeg = 120f;  // + escursione
                break;
            case ExperimentRT.Stretch.Stretch2:
                MaxAngleDeg = 150f;  // ++ escursione
                break;
        }
        // opzionale: puoi variare anche il gioco per condizione, se vuoi
        // es: softExtraDeg = (s == ExperimentRT.Stretch.Baseline) ? 10f : (s == ExperimentRT.Stretch.Stretch1 ? 12f : 14f);
    }

    [ContextMenu("Calibra Neutral = distanza attuale")]
    public void CalibrateNeutral()
    {
        if (puntoA == null || puntoB == null) return;
        neutralDistanceCm = Vector3.Distance(puntoA.position, puntoB.position) * 100f;
        Debug.Log($"{name}/Gripper: neutralDistanceCm = {neutralDistanceCm:0.0} cm");
    }

    [ContextMenu("Calibra Open = distanza attuale")]
    public void CalibrateOpen()
    {
        if (puntoA == null || puntoB == null) return;
        openDistanceCm = Vector3.Distance(puntoA.position, puntoB.position) * 100f;
        Debug.Log($"{name}/Gripper: openDistanceCm = {openDistanceCm:0.0} cm");
    }
}
