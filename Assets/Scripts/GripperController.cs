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

    [SerializeField]
    [Tooltip("Angolo massimo (deg) quando si è all'openDistanceCm")]
    private float maxAngleDeg = 90f; // <- baseline di default

    /// <summary>
    /// Property per compatibilità con ExperimentRT (setter usato per 90/120/150).
    /// </summary>
    public float MaxAngleDeg
    {
        get => maxAngleDeg;
        set => maxAngleDeg = value;
    }

    [Header("Direzione / segno")]
    [Tooltip("Se l'angolo va nella direzione opposta, inverti")]
    public bool invertAngle = false;

    [Header("Smoothing")]
    [Tooltip("Tempo di smussamento (s) verso il nuovo angolo")]
    public float smoothTime = 0.05f;

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

        // angolo desiderato
        float angle = t * maxAngleDeg;
        if (invertAngle) angle = -angle;

        // clamp "di sicurezza"
        float hardClamp = Mathf.Abs(maxAngleDeg) * 2f;
        angle = Mathf.Clamp(angle, -hardClamp, hardClamp);

        // smoothing
        currentAngle = Mathf.SmoothDamp(currentAngle, angle, ref angleVel, smoothTime);

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
    }

    // --- Utility di calibrazione dal menu di context ---
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
