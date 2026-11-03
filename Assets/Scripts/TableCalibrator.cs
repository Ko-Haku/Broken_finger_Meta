using UnityEngine;
using System.Collections;

/// <summary>
/// Usa la posizione del joystickAnchor (il controller fisico dentro il bicchiere,
/// posato sul tavolo reale) per calibrare automaticamente l'altezza del player XR.
///
/// PROBLEMA COMUNE: se calibrateOnStart è true, il tracking XR potrebbe non essere
/// ancora stabile al primo frame → il joystick riporta Y=0 → delta enorme → player a 70m.
/// SOLUZIONE: calibrationDelaySeconds aspetta che il tracking sia inizializzato.
///
/// SETUP nell'Inspector:
///   - xrRigRoot          → il GameObject radice del rig XR
///   - cameraOffsetObject → il figlio Camera Offset / TrackingSpace
///   - joystickAnchor     → lo stesso Transform usato in CupSwitchDriver2Bases
///   - tableHeightAboveFloor → altezza REALE del tavolo in metri (es. 0.76)
///   - cupHeightOnTable   → altezza del centro bicchiere sul tavolo (es. 0.05)
///   - calibrateOnStart   → calibra automaticamente a gioco avviato (dopo il delay)
///   - calibrationDelaySeconds → secondi di attesa prima di campionare (default 2s)
///   - calibrateKey       → tasto per ricalibrare a runtime (default: C)
///   - nudgeKey / nudgeStep → frecce Su/Giù per fine-tuning senza rientrare nell'editor
/// </summary>
public class TableCalibrator : MonoBehaviour
{
    [Header("XR Rig")]
    [Tooltip("Radice del rig XR (XR Origin, OVR Camera Rig…).\n" +
             "Usato come fallback se cameraOffsetObject non è assegnato.")]
    public Transform xrRigRoot;

    [Tooltip("Il Transform da spostare per la calibrazione (Camera Offset, TrackingSpace, o la root stessa).\n" +
             "Se hai un solo GameObject per tutto, assegnalo QUI e lascia xrRigRoot vuoto — o metti lo stesso in entrambi, non cambia nulla.")]
    public Transform cameraOffsetObject;

    [Header("Riferimento tavolo")]
    [Tooltip("Il joystickAnchor già usato in CupSwitchDriver2Bases (controller nel bicchiere, posato sul tavolo).")]
    public Transform joystickAnchor;

    [Tooltip("Il Transform del bicchiere (cup). Se assegnato, la calibrazione allinea il CUP alla base " +
             "invece del joystick (che potrebbe essere decentrato dentro il bicchiere).\n" +
             "Se non assegnato, usa joystickAnchor come prima.")]
    public Transform cupTransform;

    [Tooltip("Se true, usa direttamente la posizione di verticalTarget (es. Base1) come riferimento Y " +
             "invece di tableHeightAboveFloor + cupHeightOnTable. Molto più pratico.")]
    public bool useTargetAsVerticalRef = true;

    [Tooltip("Il Transform che il bicchiere deve matchare in altezza (es. Base1).\n" +
             "Se useTargetAsVerticalRef = true, il rig viene spostato in Y in modo che il joystick sia alla stessa Y di questo oggetto.")]
    public Transform verticalTarget;

    [Tooltip("Se true, usa le Renderer.bounds di cupTransform e verticalTarget per allineare automaticamente " +
             "il FONDO del bicchiere al TOP della base (così si toccano). Se false, pivot-to-pivot come prima.")]
    public bool autoStackOnBase = true;

    [Tooltip("Altezza REALE del tavolo in metri dal pavimento fisico (es. 0.76 m).\n" +
             "Usato SOLO se useTargetAsVerticalRef = false.")]
    public float tableHeightAboveFloor = 0.76f;

    [Tooltip("Eventuale offset aggiuntivo in Y se il bicchiere non è esattamente sul piano del tavolo " +
             "(es. 0.05 = centro del bicchiere è 5 cm sopra il tavolo).\n" +
             "Usato SOLO se useTargetAsVerticalRef = false.")]
    public float cupHeightOnTable = 0.05f;

    [Header("Allineamento orizzontale (opzionale)")]
    [Tooltip("Se true, allinea anche X e Z del rig in modo che il joystick coincida con horizontalTarget (o verticalTarget).")]
    public bool horizontalAlign = true;

    [Tooltip("Il Transform di riferimento orizzontale: tipicamente Base1.\n" +
             "Se non assegnato, usa verticalTarget come fallback.")]
    public Transform horizontalTarget;

    [Tooltip("Offset X/Z aggiuntivo (metri) rispetto a horizontalTarget, se serve un piccolo aggiustamento.")]
    public Vector2 horizontalOffset = Vector2.zero;

    [Header("Calibrazione")]
    [Tooltip("Esegue la calibrazione automaticamente all'avvio della scena.")]
    public bool calibrateOnStart = true;

    [Tooltip("Secondi di attesa prima di campionare il joystick all'avvio.\n" +
             "IMPORTANTE: il tracking XR impiega 1-3 secondi a stabilizzarsi dopo Play.\n" +
             "Se vedi il player teletrasportato a decine di metri, aumenta questo valore.")]
    public float calibrationDelaySeconds = 2.5f;

    [Tooltip("Tasto per ricalibrazione manuale a runtime (default: C).")]
    public KeyCode calibrateKey = KeyCode.R;

    [Header("Fine-tuning altezza in-game")]
    [Tooltip("Se true, usa i tasti NudgeUp/NudgeDown per aggiustare l'offset Y senza uscire dall'editor.")]
    public bool enableNudge = true;

    [Tooltip("Tasto per alzare il rig di nudgeStep metri.")]
    public KeyCode nudgeUpKey = KeyCode.UpArrow;

    [Tooltip("Tasto per abbassare il rig di nudgeStep metri.")]
    public KeyCode nudgeDownKey = KeyCode.DownArrow;

    [Tooltip("Quanto si sposta il rig ad ogni pressione (metri). Default 0.01 = 1 cm.")]
    public float nudgeStep = 0.01f;

    [Tooltip("Se true, mostra un log con i valori calcolati.")]
    public bool verboseLog = true;

    // ─── Stato ───────────────────────────────────────────────────────────────
    private bool _calibrated = false;
    private float _totalNudgeY = 0f;        // accumulatore fine-tuning

    // ─────────────────────────────────────────────────────────────────────────
    private void Start()
    {
        if (calibrateOnStart)
        {
            if (calibrationDelaySeconds > 0f)
                StartCoroutine(DelayedCalibrate());
            else
                Calibrate();
        }
    }

    private IEnumerator DelayedCalibrate()
    {
        if (verboseLog)
            Debug.Log($"[TableCalibrator] Attendo {calibrationDelaySeconds:F1}s per stabilizzazione tracking...");

        yield return new WaitForSeconds(calibrationDelaySeconds);
        Calibrate();
    }

    private void Update()
    {
        if (Input.GetKeyDown(calibrateKey))
        {
            Debug.Log("[TableCalibrator] Ricalibrazione manuale richiesta.");
            Calibrate();
        }

        if (enableNudge)
        {
            if (Input.GetKeyDown(nudgeUpKey))   NudgeY(+nudgeStep);
            if (Input.GetKeyDown(nudgeDownKey)) NudgeY(-nudgeStep);
        }
    }

    // ─── API pubblica ─────────────────────────────────────────────────────────

    /// <summary>
    /// Esegue la calibrazione: sposta cameraOffsetObject (o xrRigRoot) in Y
    /// in modo che il joystick virtuale sia all'altezza reale del tavolo.
    /// </summary>
    public void Calibrate()
    {
        if (joystickAnchor == null)
        {
            Debug.LogError("[TableCalibrator] joystickAnchor non assegnato! Impossibile calibrare.");
            return;
        }

        // Unico target: cameraOffsetObject se assegnato, altrimenti xrRigRoot.
        // Se hai messo lo stesso GameObject in entrambi i campi va benissimo: viene usato una volta sola.
        Transform target = cameraOffsetObject != null ? cameraOffsetObject : xrRigRoot;

        if (target == null)
        {
            Debug.LogError("[TableCalibrator] xrRigRoot (o cameraOffsetObject) non assegnato!");
            return;
        }

        float joystickWorldY = joystickAnchor.position.y;

        // Punto di riferimento per l'allineamento: il bicchiere se assegnato, altrimenti il joystick
        Transform alignRef = cupTransform != null ? cupTransform : joystickAnchor;
        Vector3 alignRefPos = alignRef.position;

        // Guardia: tracking non ancora inizializzato
        if (Mathf.Abs(joystickWorldY) < 0.001f)
        {
            Debug.LogWarning($"[TableCalibrator] joystickAnchor.y ≈ 0 ({joystickWorldY:F4} m): " +
                             $"il tracking potrebbe non essere ancora attivo. " +
                             $"Riprovo tra 1s. Aumenta calibrationDelaySeconds se continua.");
            StartCoroutine(RetryCalibrate(1f));
            return;
        }

        // ── Calcola tutti i delta ──────────────────────────────────────────────
        float targetWorldY;
        if (useTargetAsVerticalRef && verticalTarget != null)
        {
            if (autoStackOnBase && cupTransform != null &&
                TryGetRendererTopY(verticalTarget, out float baseTopY) &&
                TryGetRendererBottomOffset(cupTransform, out float cupBottomOffset))
            {
                // Voglio: cupBottomWorld = baseTopWorld
                //         cupTransform.y + cupBottomOffset = baseTopY
                //         cupTransform.y = baseTopY - cupBottomOffset
                targetWorldY = baseTopY - cupBottomOffset;
                if (verboseLog)
                    Debug.Log($"[TableCalibrator] autoStack: baseTopY={baseTopY:F4} cupBottomOffset={cupBottomOffset:F4} → targetCupY={targetWorldY:F4}");
            }
            else
            {
                // Pivot-to-pivot: cup.y = base.y
                targetWorldY = verticalTarget.position.y;
            }
        }
        else
        {
            // Vecchio metodo: altezza tavolo + offset bicchiere
            targetWorldY = tableHeightAboveFloor + cupHeightOnTable;
        }

        float deltaY = targetWorldY - alignRefPos.y;

        if (Mathf.Abs(deltaY) > 5f)
        {
            Debug.LogWarning($"[TableCalibrator] deltaY = {deltaY:F2} m → troppo grande! " +
                             $"alignRef Y={alignRefPos.y:F4}, target={targetWorldY:F4}. " +
                             $"Calibrazione ANNULLATA. Controlla i valori o aumenta il delay.");
            return;
        }

        float deltaX = 0f, deltaZ = 0f;
        if (horizontalAlign)
        {
            Transform hRef = horizontalTarget != null ? horizontalTarget : verticalTarget;
            if (hRef == null)
            {
                Debug.LogWarning("[TableCalibrator] horizontalAlign = true ma nessun target assegnato! Allineamento X/Z saltato.");
            }
            else
            {
                deltaX = (hRef.position.x - alignRefPos.x) + horizontalOffset.x;
                deltaZ = (hRef.position.z - alignRefPos.z) + horizontalOffset.y;
            }
        }

        // ── Applica tutto in una sola assegnazione ────────────────────────────
        target.position += new Vector3(deltaX, deltaY, deltaZ);

        _calibrated  = true;
        _totalNudgeY = 0f;

        if (verboseLog)
        {
            Debug.Log(
                $"[TableCalibrator] ✓ Calibrazione applicata su '{target.name}' (ref='{alignRef.name}').\n" +
                $"  ΔY = {deltaY:+0.0000;-0.0000} m  (ref Y={alignRefPos.y:F4}, target={targetWorldY:F4})\n" +
                $"  ΔX = {deltaX:+0.0000;-0.0000} m  ΔZ = {deltaZ:+0.0000;-0.0000} m  (horizontalAlign={horizontalAlign})\n" +
                $"  nuova posizione rig = {target.position}"
            );
        }
    }

    /// <summary>
    /// Sposta il rig di 'amount' metri in Y (fine-tuning senza rientrare nell'editor).
    /// Il totale accumulato viene loggato così puoi aggiornare cupHeightOnTable in modo permanente.
    /// </summary>
    public void NudgeY(float amount)
    {
        Transform target = cameraOffsetObject != null ? cameraOffsetObject : xrRigRoot;
        if (target == null) return;

        target.position += new Vector3(0f, amount, 0f);
        _totalNudgeY    += amount;

        if (verboseLog)
            Debug.Log($"[TableCalibrator] Nudge Y {(amount >= 0 ? "+" : "")}{amount * 100f:F1} cm " +
                      $"| totale nudge = {_totalNudgeY * 100f:F1} cm " +
                      $"→ per rendere permanente imposta cupHeightOnTable += {_totalNudgeY:F4}");
    }

    // ─── Gizmo (solo editor) ──────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (joystickAnchor == null) return;

        float targetY;
        if (useTargetAsVerticalRef && verticalTarget != null)
            targetY = verticalTarget.position.y;
        else
            targetY = tableHeightAboveFloor + cupHeightOnTable;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(joystickAnchor.position, new Vector3(joystickAnchor.position.x, targetY, joystickAnchor.position.z));

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.4f);
        Gizmos.DrawWireCube(
            new Vector3(joystickAnchor.position.x, targetY, joystickAnchor.position.z),
            new Vector3(0.5f, 0.001f, 0.5f)
        );

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(joystickAnchor.position, 0.015f);
    }
#endif

    private IEnumerator RetryCalibrate(float delay)
    {
        yield return new WaitForSeconds(delay);
        Calibrate();
    }

    // ─── Helpers per auto-stack ──────────────────────────────────────────────

    private static bool TryGetRendererBounds(Transform t, out Bounds b)
    {
        b = default;
        if (t == null) return false;
        var renderers = t.GetComponentsInChildren<Renderer>();
        bool has = false;
        foreach (var r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        return has;
    }

    private static bool TryGetRendererTopY(Transform t, out float topY)
    {
        topY = 0f;
        if (!TryGetRendererBounds(t, out var b)) return false;
        topY = b.max.y;
        return true;
    }

    // Returns the signed offset from t.position.y to the bottom of its renderer bounds
    // (usually negative: cup bottom is below cup pivot).
    private static bool TryGetRendererBottomOffset(Transform t, out float offset)
    {
        offset = 0f;
        if (!TryGetRendererBounds(t, out var b)) return false;
        offset = b.min.y - t.position.y;
        return true;
    }
}

