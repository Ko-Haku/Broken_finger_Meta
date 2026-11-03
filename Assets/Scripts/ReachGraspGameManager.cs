using System;
using System.Collections;
using UnityEngine;
using TriggerStation_Unity3d_x64;

public enum TrialPhase
{
    None,
    Baseline,            // prima del trigger 1 (beep start → panel open)
    MotorPlanning,       // trigger 1 (panel open) → trigger 2 (GO)
    ReachToGrasp,        // trigger 2 (GO) → trigger 3 (STOP)
    Release,             // trigger 3 (STOP) → trigger 4 (Restore)
    PostMovement,        // trigger 4 (Restore) → chiusura pannello
    InterTrialInterval   // auto-close pannello / pausa tra trial
}

public class ReachGraspGameManager : MonoBehaviour
{
    // -------------------- TriggerStation --------------------
    private readonly TriggerStationDevice TriggerStation = new TriggerStationDevice();

    // -------------------- Experiment Parameters --------------------
    [Header("Experiment Parameters")]
    public int totalTrials = 40;

    [Header("Timing (nuova logica trial) — tempi RELATIVI a Panel Open")]
    public float warningAfterOpenSeconds = 1.5f;
    public float goAfterOpenSeconds = 3.0f;
    public float stopAfterOpenSeconds = 5.0f;
    [Tooltip("Tempo (centro) del beep RESTORE relativo a Panel Open. " +
             "Effettivo per-trial = restoreAfterOpenSeconds ± restoreJitterSeconds. " +
             "Default 8.5s con jitter 0.5s → gap STOP→RESTORE uniforme in [3.0, 4.0] s.")]
    public float restoreAfterOpenSeconds = 8.5f;
    [Tooltip("Jitter random (±) applicato al tempo di RESTORE per ridurre l'aspettativa del beep restore. " +
             "0 = niente jitter; il jitter è scelto una volta per trial.")]
    public float restoreJitterSeconds = 0.5f;
    [Tooltip("Pitch del beep RESTORE (1.0 = originale; < 1.0 = più basso di tono; > 1.0 = più alto). " +
             "Default 0.85 per distinguerlo dagli altri beep e ridurre l'allerta.")]
    [Range(0.3f, 2.0f)]
    public float restoreBeepPitch = 0.85f;

    [Header("Between-trials")]
    [Tooltip("Tempo fisso tra beep start trial e apertura pannello.")]
    public float openPanelDelayFromStartBeep = 2.0f;

    [Tooltip("Tempo dopo il beep RESTORE prima di chiudere automaticamente il pannello.")]
    public float closePanelDelayAfterRestore = 3.0f;

    [Header("Button press handling (fallback manuale, opzionale)")]
    public float pressDebounceSeconds = 0.15f;

    // -------------------- Scene References --------------------
    [Header("Scene References")]
    public GameObject fixationCross;
    public BlackPanelBehaviour blackPanel;


    // -------------------- Audio --------------------
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip beepGeneric;   // inizio trial + restore (audio disattivato — solo trigger/log)
    public AudioClip beepWarning;   // cue di preparazione a panel+warningAfterOpenSeconds (S1 uditivo)
    public AudioClip beepGo;
    public AudioClip beepStop;

    // -------------------- Logging + Cup --------------------
    public enum EffectorType { Hand, Tweezers, TweezersGrasp }

    [Header("Logging")]
    public ExperimentLogger logger;
    public EffectorType effectorType = EffectorType.Hand;

    [Header("Effector GameObjects")]
    [Tooltip("GameObject da accendere quando effectorType = Hand (es. mano/hand tracking)")]
    public GameObject handEffectorGO;
    [Tooltip("GameObject da accendere quando effectorType = Tweezers (pinzetta, presa a pinch)")]
    public GameObject tweezersEffectorGO;
    [Tooltip("GameObject da accendere quando effectorType = TweezersGrasp (pinzetta, presa a grasp)")]
    public GameObject tweezersGraspEffectorGO;

    [Header("Effector tip transforms (end-effector per cinematica)")]
    [Tooltip("Transform della tip da loggare quando Hand è attivo (es. XRHand_IndexTip).")]
    public Transform handEffectorTip;
    [Tooltip("Transform della tip della pinza pinch (es. IndexMiddleFingerTip_low).")]
    public Transform tweezersEffectorTip;
    [Tooltip("Transform della tip della pinza grasp (es. pinza1).")]
    public Transform tweezersGraspEffectorTip;

    [Header("CCT (owner delle palline stim — opzionale, per sync visibilità)")]
    [Tooltip("Se assegnato, sincronizza la visibilità delle palline stim al cambio effettore.")]
    public CCT_ReachExperiment cctReachExperiment;

    [Header("Cup driver")]
    public CupSwitchDriver2Bases cupDriver;

    [Header("Hand kinematics (velocity profile)")]
    [Tooltip("Optional: logs hand position + velocity every frame during trials.")]
    public HandKinematicsLogger kinematicsLogger;

    [Header("Bad-trial detection")]
    [Tooltip("If true, BAD_TRIAL events are emitted with the accumulated flags. " +
             "If false, the metrics (MOTION_ONSET delay, rel timings, ecc.) are still logged " +
             "but no trial is marked bad — la flagging si fa in post-processing dal CSV.")]
    public bool enableBadTrialFlags = false;
    [Tooltip("Wrist transform — its velocity is monitored to detect motion onset (es. XRHand_Wrist).")]
    public Transform wristTransform;
    [Tooltip("Motion onset = wrist speed sustained above this threshold (m/s). 0.05 = 5 cm/s.")]
    public float motionOnsetVelocityMps = 0.05f;
    [Tooltip("Sustained duration (ms) above velocity threshold required to confirm motion onset. " +
             "Filters out tracking jitter / tiny postural drifts.")]
    public float motionOnsetSustainMs = 50f;
    [Tooltip("Max delay (ms) from BEEP_GO to motion onset before flagging motion_too_late.")]
    public float motionLateThresholdMs = 500f;
    [Tooltip("Max delay (ms) from BEEP_STOP to release2 before flagging return_too_late.")]
    public float returnLateThresholdMs = 500f;

    // -------------------- Debug / Stato --------------------
    [Header("Debug / Stato")]
    [SerializeField] private TrialPhase currentPhase = TrialPhase.None;
    [SerializeField] private int currentTrial = 0;

    [SerializeField] private bool experimentRunning = false;
    [SerializeField] private bool handOnButton = false; // compatibilità/Inspector

    // ---- Internal press event state ----
    private int _buttonPressCount = 0;
    private float _lastPressTime = -999f;

    // ---- Trial timing state (Stopwatch ms) ----
    private long _trialStartBeepMs = -1;
    private long _t0PanelOpenMs = -1;
    private long _goBeepMs = -1;
    private long _stopBeepMs = -1;

    // ---- Trial events ----
    private bool _trialActive = false;
    private int _grabCount = 0;
    private int _releaseCount = 0;
    private long _grab1Ms = -1, _rel1Ms = -1, _grab2Ms = -1, _rel2Ms = -1;

    // ---- Quality ----
    private bool _trialBad = false;
    private readonly System.Collections.Generic.List<string> _trialBadFlags = new System.Collections.Generic.List<string>();

    // ---- Motion onset tracking (velocity-based) ----
    private long _motionOnsetMs = -1;


    // ---- Effector visuals update (edge detection) ----
    private EffectorType _lastEffectorType;

    // ---- Pause ----
    [SerializeField] private bool isPaused = false;

    // -------------------- Unity lifecycle --------------------
    private void Start()
    {
        // TriggerStation init
        TriggerStation.InitTriggerStationUnity3d_DLL();
        int tsDevices = TriggerStation.EnumerateDevices();
        Debug.Log("Devices: " + tsDevices);
        bool tsConnected = false;
        if (tsDevices > 0)
        {
            tsConnected = TriggerStation.ConnectToDevice_Serial(33471);
            if (!tsConnected)
            {
                Debug.LogWarning("[TriggerStation] serial 33471 non matcha, fallback a Index 0");
                tsConnected = TriggerStation.ConnectToDevice_Index(0);
            }
        }
        Debug.Log("Connected: " + tsConnected);

        // Subscribe eventi bicchiere
        if (cupDriver != null)
        {
            cupDriver.OnCupPickedUp += OnCupPickedUp;
            cupDriver.OnCupDocked += OnCupDocked;
        }
        else
        {
            Debug.LogWarning("[ReachGraspGameManager] cupDriver non assegnato in Inspector.");
        }


        // init effector visuals — assegna un valore sicuramente diverso da qualsiasi membro dell'enum per forzare il primo refresh
        _lastEffectorType = (EffectorType)(-1);
        RefreshEffectorIfChanged();

        // NON auto-start: lo fai con SPACE in Update()
        // StartExperiment();
    }

    private void OnDestroy()
    {
        if (cupDriver != null)
        {
            cupDriver.OnCupPickedUp -= OnCupPickedUp;
            cupDriver.OnCupDocked -= OnCupDocked;
        }
    }

    // -------------------- Public control --------------------
    public void StartExperiment()
    {
        if (experimentRunning) return;
        experimentRunning = true;
        LogEvent("EXPERIMENT_START", "");
        if (kinematicsLogger != null) kinematicsLogger.StartRecording();
        StartCoroutine(ExperimentRoutine());
    }

    /// <summary>
    /// Chiamata dal bottone giallo: qui la usiamo SOLO in InterTrialInterval come "fine trial".
    /// </summary>
    public void SetHandOnButton()
    {
        if (currentPhase != TrialPhase.InterTrialInterval) return;

        if (Time.time - _lastPressTime < pressDebounceSeconds) return;
        _lastPressTime = Time.time;

        _buttonPressCount++;
        handOnButton = true;

        Debug.Log($"[Button] Close Press #{_buttonPressCount}");
        LogEvent("BUTTON_CLOSE_PRESSED", $"pressCount={_buttonPressCount}");
    }

    // -------------------- Core routines --------------------
    private IEnumerator ExperimentRoutine()
    {
        for (currentTrial = 1; currentTrial <= totalTrials; currentTrial++)
            yield return StartCoroutine(SingleTrialRoutine(currentTrial));

        experimentRunning = false;
        currentPhase = TrialPhase.None;
        isPaused = false;
        Time.timeScale = 1f;
        SyncKinematics();
        if (kinematicsLogger != null) kinematicsLogger.StopRecording();

        Debug.Log("Esperimento terminato.");
        LogEvent("EXPERIMENT_END", "");
    }

    private IEnumerator SingleTrialRoutine(int trialIndex)
    {
        Debug.Log($"--- TRIAL {trialIndex} ---");

        // reset trial state
        _trialActive = true;
        _trialBad = false;
        _trialBadFlags.Clear();

        _grabCount = 0;
        _releaseCount = 0;
        _grab1Ms = _rel1Ms = _grab2Ms = _rel2Ms = -1;

        _trialStartBeepMs = -1;
        _t0PanelOpenMs = -1;
        _goBeepMs = -1;
        _stopBeepMs = -1;

        _motionOnsetMs = -1;

        handOnButton = false;

        if (fixationCross != null) fixationCross.SetActive(true);
        if (blackPanel != null) blackPanel.ClosePanel();


        LogEvent("TRIAL_START", $"index={trialIndex}");

        // 1) beep start trial — BASELINE (prima del trigger 1)
        currentPhase = TrialPhase.Baseline;
        SyncKinematics();
        // PlayBeep(beepGeneric); // audio start-trial disattivato (riattiva se vuoi reintrodurre il cue uditivo iniziale)

        _trialStartBeepMs = NowMs();
        LogEvent("BEEP_TRIAL_START", $"sw_ms={_trialStartBeepMs}");

        // 2) apertura pannello dopo delay fisso — TRIGGER 1 → MOTOR PLANNING
        yield return new WaitForSeconds(openPanelDelayFromStartBeep);

        currentPhase = TrialPhase.MotorPlanning;
        SyncKinematics();

        if (blackPanel != null) blackPanel.OpenPanel();
        StartCoroutine(TriggerandWait(0.1f));

        _t0PanelOpenMs = NowMs();
        LogEvent("PANEL_OPEN", $"sw_ms={_t0PanelOpenMs}");

        // "t0" per lo scheduling Unity (relativo a panel open) — stesso frame
        float t0 = Time.time;

        // 2.5) WARNING — beep+trigger di preparazione (S1 uditivo) durante MotorPlanning
        float warningTime = t0 + warningAfterOpenSeconds;
        while (Time.time < warningTime) yield return null;

        PlayBeep(beepWarning);
        StartCoroutine(TriggerandWait(0.1f));
        LogEvent("BEEP_WARNING", $"sw_ms={NowMs()}");

        // 3) GO — TRIGGER 2 → REACH TO GRASP
        float goTime = t0 + goAfterOpenSeconds;
        while (Time.time < goTime) yield return null;

        currentPhase = TrialPhase.ReachToGrasp;
        SyncKinematics();

        PlayBeep(beepGo);
        StartCoroutine(TriggerandWait(0.1f));
        _goBeepMs = NowMs();
        LogEvent("BEEP_GO", $"sw_ms={_goBeepMs}");

        // Motion-onset watcher: track wrist velocity from BEEP_GO.
        if (wristTransform != null) StartCoroutine(WatchMotionOnset());

        // 5) STOP — TRIGGER 3 → RELEASE
        float stopTime = t0 + stopAfterOpenSeconds;
        while (Time.time < stopTime) yield return null;

        currentPhase = TrialPhase.Release;
        SyncKinematics();

        PlayBeep(beepStop);
        StartCoroutine(TriggerandWait(0.1f));

        _stopBeepMs = NowMs();
        LogEvent("BEEP_STOP", $"sw_ms={_stopBeepMs}");

        // 6) RESTORE — TRIGGER 4 → POST MOVEMENT
        float restoreJitter = restoreJitterSeconds > 0f
            ? UnityEngine.Random.Range(-restoreJitterSeconds, restoreJitterSeconds)
            : 0f;
        float restoreTime = t0 + Mathf.Max(stopAfterOpenSeconds, restoreAfterOpenSeconds + restoreJitter);
        while (Time.time < restoreTime) yield return null;

        currentPhase = TrialPhase.PostMovement;
        SyncKinematics();

        PlayBeep(beepGeneric, restoreBeepPitch);
        StartCoroutine(TriggerandWait(0.1f));
        LogEvent("BEEP_RESTORE", $"sw_ms={NowMs()}");

        // 7) chiusura automatica pannello dopo delay (oppure pulsante manuale se premuto prima)
        currentPhase = TrialPhase.InterTrialInterval;
        SyncKinematics();
        handOnButton = false;

        yield return StartCoroutine(WaitForAutoCloseOrButton(closePanelDelayAfterRestore));

        if (blackPanel != null) blackPanel.ClosePanel();
        LogEvent("PANEL_CLOSE", $"sw_ms={NowMs()}");

        // summary + end
        _trialActive = false;

        // ── Bad-trial flags (multipli, accumulati) ──
        // 1) motion never detected (wrist never moved 10 cm closer to cup)
        if (_motionOnsetMs < 0 && wristTransform != null)
            _trialBadFlags.Add("motion_never_detected");

        // 2) rel2 timing relative to BEEP_STOP
        if (_rel2Ms > 0 && _stopBeepMs > 0)
        {
            long retDelay = _rel2Ms - _stopBeepMs;
            if (retDelay < 0)
                _trialBadFlags.Add($"release_before_stop;ms={retDelay}");
            else if (retDelay > returnLateThresholdMs)
                _trialBadFlags.Add($"return_too_late;ms={retDelay}");
        }

        // 3) cup still in hand at trial end (no release for the last grab)
        if (_grabCount > _releaseCount)
            _trialBadFlags.Add($"stuck_in_hand;grabs={_grabCount};releases={_releaseCount}");

        // 4) missing release2 (legacy criterion)
        if (_releaseCount < 2)
            _trialBadFlags.Add($"missing_release2;releases={_releaseCount}");

        // Bad-trial event only if flagging is enabled. Le metriche restano comunque
        // nel TRIAL_SUMMARY (motionOnset, motionDelayMs, grab/rel timings) per la
        // flagging in post-processing dal CSV.
        if (enableBadTrialFlags && _trialBadFlags.Count > 0)
        {
            _trialBad = true;
            LogEvent("BAD_TRIAL", "flags=" + string.Join("|", _trialBadFlags));
        }

        long motionDelayLog = (_motionOnsetMs > 0 && _goBeepMs > 0)
            ? (_motionOnsetMs - _goBeepMs) : -1;
        LogEvent("TRIAL_SUMMARY",
            $"grab1={_grab1Ms};rel1={_rel1Ms};grab2={_grab2Ms};rel2={_rel2Ms};" +
            $"motionOnset={_motionOnsetMs};motionDelayMs={motionDelayLog};bad={_trialBad}");

        LogEvent("TRIAL_END", $"index={trialIndex};bad={_trialBad}");

        currentPhase = TrialPhase.None;
        SyncKinematics();
        Debug.Log($"--- FINE TRIAL {trialIndex} ---");
    }

    /// <summary>
    /// Per-frame watch from BEEP_GO until first grab (or trial end):
    /// computes wrist velocity each frame; when speed stays above
    /// motionOnsetVelocityMps for motionOnsetSustainMs, declares MOTION_ONSET.
    /// Flags motion_too_late if delay from BEEP_GO exceeds threshold.
    /// </summary>
    private IEnumerator WatchMotionOnset()
    {
        if (wristTransform == null || _goBeepMs < 0) yield break;

        Vector3 prevPos = wristTransform.position;
        float prevTime = Time.time;
        float sustainedMs = 0f;
        float peakV = 0f;

        while (_trialActive && _grabCount == 0 && _motionOnsetMs < 0)
        {
            yield return null;

            float dt = Time.time - prevTime;
            if (dt <= 0f) continue;

            Vector3 curPos = wristTransform.position;
            float v = (curPos - prevPos).magnitude / dt;
            prevPos = curPos;
            prevTime = Time.time;

            if (v > peakV) peakV = v;

            if (v >= motionOnsetVelocityMps)
            {
                sustainedMs += dt * 1000f;
                if (sustainedMs >= motionOnsetSustainMs)
                {
                    _motionOnsetMs = NowMs();
                    long delay = _motionOnsetMs - _goBeepMs;
                    LogEvent("MOTION_ONSET",
                        $"sw_ms={_motionOnsetMs};delay_ms={delay};vel_mps={v:F3};peakV_mps={peakV:F3}");
                    if (delay > motionLateThresholdMs)
                        _trialBadFlags.Add($"motion_too_late;delay_ms={delay}");
                    yield break;
                }
            }
            else
            {
                sustainedMs = 0f;
            }
        }
    }

    /// <summary>
    /// Attende automaticamente il delay oppure si interrompe prima se il pulsante viene premuto.
    /// </summary>
    private IEnumerator WaitForAutoCloseOrButton(float maxWait)
    {
        int startCount = _buttonPressCount;
        float elapsed = 0f;
        while (elapsed < maxWait && _buttonPressCount == startCount)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_buttonPressCount > startCount)
            LogEvent("PANEL_CLOSE_MANUAL", $"elapsed={elapsed:F2}");
        else
            LogEvent("PANEL_CLOSE_AUTO", $"delay={maxWait:F2}");
    }

    // -------------------- Trigger logging --------------------
    public IEnumerator TriggerandWait(float seconds)
    {
        TriggerStation.BNC2(true);
        LogEvent("TRIGGER_BNC2", "ON");

        yield return new WaitForSeconds(seconds);

        TriggerStation.BNC2(false);
        LogEvent("TRIGGER_BNC2", "OFF");
    }

    [ContextMenu("Test Trigger BNC2 (0.1s)")]
    private void TestTriggerBNC2()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[TestTrigger] Richiede Play mode.");
            return;
        }
        Debug.Log("[TestTrigger] Pulso BNC2 0.1s");
        StartCoroutine(TriggerandWait(0.1f));
    }

    // -------------------- Hand-table trigger callbacks (da HandTableTrigger) --------------------

    /// <summary>
    /// Chiamato da HandTableTrigger quando la mano entra nel BoxCollider del tavolo.
    /// Avvia il trial cinematico: da questo momento HandKinematicsLogger scrive dati.
    /// </summary>
    public void OnHandEnterTable()
    {
        if (!_trialActive) return;
        long ms = NowMs();
        // Imposta trial e fase → il logger inizia a registrare (onlyDuringTrials richiede trial > 0)
        if (kinematicsLogger != null)
            kinematicsLogger.SetTrialInfo(currentTrial, "HandOnTable");
        LogEvent("HAND_ON_TABLE", $"sw_ms={ms}");
    }

    /// <summary>
    /// Chiamato da HandTableTrigger quando la mano LASCIA il BoxCollider del tavolo.
    /// Aggiorna la fase cinematica a "Reaching".
    /// </summary>
    public void OnHandExitTable()
    {
        if (!_trialActive) return;
        long ms = NowMs();
        if (kinematicsLogger != null)
            kinematicsLogger.SetTrialInfo(currentTrial, "Reaching");
        LogEvent("HAND_LEFT_TABLE", $"sw_ms={ms}");
    }

    // -------------------- Effector-Cup contact (BNC trigger) --------------------

    /// <summary>
    /// Chiamato da CupEffectorTrigger quando il collider "effector" (GripPoint)
    /// entra nel collider del bicchiere. Solo log: niente BNC trigger perché
    /// l'evento dipende dalla performance (latency variabile) e contaminerebbe i
    /// marker EEG, riservati agli eventi-stimolo automatici.
    /// </summary>
    public void OnEffectorTouchedCup()
    {
        long ms = NowMs();
        LogEvent("EFFECTOR_TOUCH_CUP", $"sw_ms={ms}");
    }

    /// <summary>
    /// Chiamato da CupEffectorTrigger quando il collider "effector" esce dal bicchiere.
    /// </summary>
    public void OnEffectorReleasedCup()
    {
        long ms = NowMs();
        LogEvent("EFFECTOR_RELEASE_CUP", $"sw_ms={ms}");
    }

    // -------------------- Cup callbacks --------------------
    private void OnCupPickedUp()
    {
        if (!_trialActive) return;

        long ms = NowMs();
        _grabCount++;

        if (_grabCount == 1) _grab1Ms = ms;
        else if (_grabCount == 2) _grab2Ms = ms;

        // Aggiorna fase cinematica → "Holding"
        if (kinematicsLogger != null)
            kinematicsLogger.SetTrialInfo(currentTrial, "Holding");

        if (_grabCount == 1 && _trialStartBeepMs > 0)
        {
            long t1 = ms - _trialStartBeepMs;
            LogEvent("CUP_GRAB_1", $"sw_ms={ms};t1_ms={t1}");
        }
        else
        {
            LogEvent($"CUP_GRAB_{_grabCount}", $"sw_ms={ms}");
        }
    }

    private void OnCupDocked(int whichBase)
    {
        if (!_trialActive) return;

        long ms = NowMs();
        _releaseCount++;

        if (_releaseCount == 1) _rel1Ms = ms;
        else if (_releaseCount == 2) _rel2Ms = ms;

        LogEvent($"CUP_RELEASE_{_releaseCount}", $"sw_ms={ms};base={whichBase}");

        // ── Fine trial cinematico: dock sulla base 2 ──
        if (whichBase == 2)
        {
            LogEvent("KINEMATIC_TRIAL_END", $"sw_ms={ms};base={whichBase}");
            // Resetta trial a 0 → onlyDuringTrials blocca ulteriore scrittura
            if (kinematicsLogger != null)
                kinematicsLogger.SetTrialInfo(0, "None");
        }
        // Quality checks (release_before_stop / return_too_late) sono valutati
        // a fine trial nel blocco bad-trial flags — niente check inline qui.
    }

    // -------------------- Helpers --------------------
    private long NowMs()
    {
        if (logger != null && logger.sw != null) return logger.sw.ElapsedMilliseconds;
        return (long)(Time.time * 1000);
    }

    private void LogEvent(string name, string value)
    {
        if (logger == null) return;

        logger.Log(
            trial: currentTrial,
            phase: currentPhase.ToString(),
            eventName: name,
            eventValue: value,
            effector: effectorType.ToString(),
            t0PanelOpenMs: _t0PanelOpenMs,
            trialStartBeepMs: _trialStartBeepMs
        );
    }


    private void RefreshEffectorIfChanged()
    {
        if (effectorType == _lastEffectorType) return;

        _lastEffectorType = effectorType;
        UpdateEffectorVisuals();
        LogEvent("EFFECTOR_CHANGED", effectorType.ToString());
    }

    [Header("Debug visualization")]
    [Tooltip("If true, handEffectorGO stays visible even during Tweezers/TweezersGrasp trials. " +
             "Lets you verify how the real (tracked) hand sits relative to the pinza model.")]
    public bool debugKeepHandVisibleAlways = false;

    private void UpdateEffectorVisuals()
    {
        bool handActive = debugKeepHandVisibleAlways || (effectorType == EffectorType.Hand);
        if (handEffectorGO != null)          handEffectorGO.SetActive(handActive);
        if (tweezersEffectorGO != null)      tweezersEffectorGO.SetActive(effectorType == EffectorType.Tweezers);
        if (tweezersGraspEffectorGO != null) tweezersGraspEffectorGO.SetActive(effectorType == EffectorType.TweezersGrasp);

        // Aggiorna la tool tip del logger cinematico (la hand resta fissa su XRHand_IndexTip
        // per coerenza tra effettori). Per effector=Hand, tool tip = hand tip.
        if (kinematicsLogger != null)
        {
            Transform toolTip = effectorType switch
            {
                EffectorType.Tweezers      => tweezersEffectorTip,
                EffectorType.TweezersGrasp => tweezersGraspEffectorTip,
                _                          => handEffectorTip,
            };
            kinematicsLogger.SetToolTipTransform(toolTip);
        }

        // Sincronizza visibilità palline stim (owner = CCT_ReachExperiment)
        if (cctReachExperiment != null)
        {
            CCT_ReachExperiment.Effector mapped;
            switch (effectorType)
            {
                case EffectorType.Tweezers:      mapped = CCT_ReachExperiment.Effector.Tweezers; break;
                case EffectorType.TweezersGrasp: mapped = CCT_ReachExperiment.Effector.TweezersGrasp; break;
                default:                         mapped = CCT_ReachExperiment.Effector.Hand; break;
            }
            cctReachExperiment.UpdateStimVisibility(mapped);
        }
    }


    private void PlayBeep(AudioClip clip, float pitch = 1f)
    {
        if (audioSource == null || clip == null) return;
        if (Mathf.Approximately(pitch, 1f))
        {
            audioSource.PlayOneShot(clip);
            return;
        }
        // PlayOneShot ignores AudioSource.pitch; route through a transient AudioSource for per-clip pitch.
        var tmp = audioSource.gameObject.AddComponent<AudioSource>();
        tmp.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        tmp.spatialBlend = audioSource.spatialBlend;
        tmp.volume = audioSource.volume;
        tmp.clip = clip;
        tmp.pitch = pitch;
        tmp.Play();
        Destroy(tmp, clip.length / Mathf.Max(0.01f, pitch) + 0.1f);
    }

    /// <summary>Pushes current trial + phase to the kinematics logger.</summary>
    private void SyncKinematics()
    {
        if (kinematicsLogger != null)
            kinematicsLogger.SetTrialInfo(currentTrial, currentPhase.ToString());
    }

    // -------------------- Pause --------------------
    private void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            Time.timeScale = 0f;
            if (audioSource != null) audioSource.Pause();
            Debug.Log($"[PAUSE] Esperimento in pausa — trial={currentTrial}, phase={currentPhase}");
            LogEvent("EXPERIMENT_PAUSED",
                $"sw_ms={NowMs()};trial={currentTrial};phase={currentPhase};grabCount={_grabCount};releaseCount={_releaseCount}");
        }
        else
        {
            Time.timeScale = 1f;
            if (audioSource != null) audioSource.UnPause();
            Debug.Log($"[PAUSE] Esperimento ripreso — trial={currentTrial}, phase={currentPhase}");
            LogEvent("EXPERIMENT_RESUMED",
                $"sw_ms={NowMs()};trial={currentTrial};phase={currentPhase}");
        }
    }

    // -------------------- Debug input --------------------
    private void Update()
    {

        // aggiorna visuals solo se effectorType cambia
        RefreshEffectorIfChanged();

        // SPACE → se esperimento fermo: avvia. Se in corso: pausa/riprendi.
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!experimentRunning)
            {
                Debug.Log("[INPUT] Start Experiment");
                StartExperiment();
            }
            else
            {
                TogglePause();
            }
            return;
        }

        // E → cicla tra effettori (Hand → Tweezers → TweezersGrasp → Hand…)
        if (Input.GetKeyDown(KeyCode.E))
        {
            CycleEffector();
            return;
        }

        // ENTER → simula pressione pulsante fine trial (debug/fallback)
        if (experimentRunning && !isPaused && Input.GetKeyDown(KeyCode.Return))
        {
            SetHandOnButton();
        }

        // B → pulso test BNC2 (libero, non lega ai trial)
        if (Input.GetKeyDown(KeyCode.B))
        {
            Debug.Log("[INPUT] Test Trigger BNC2");
            StartCoroutine(TriggerandWait(0.1f));
        }
    }

    private void CycleEffector()
    {
        switch (effectorType)
        {
            case EffectorType.Hand:           effectorType = EffectorType.Tweezers;      break;
            case EffectorType.Tweezers:       effectorType = EffectorType.TweezersGrasp; break;
            case EffectorType.TweezersGrasp:  effectorType = EffectorType.Hand;          break;
        }
        Debug.Log($"[INPUT] Effector → {effectorType}");
        UpdateEffectorVisuals();
    }
}
