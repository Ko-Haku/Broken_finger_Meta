using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Secondo esperimento: Reach-only + CCT per ciascuno dei 3 effettori.
/// Da eseguire DOPO il main experiment (ReachGraspGameManager).
///
/// Flusso:
///   1. Baseline CCT (mano visibile, 0 reach) → 20 trial
///   2. Per ogni effettore (ordine controbilanciato):
///      a) N trial reach-only (tocca il bicchiere, non afferrare)
///      b) 20 trial CCT
///   Totale: 80 CCT + 3×N reach
///
/// Avvio: tasto T (configurabile).
/// </summary>
public class CCT_ReachExperiment : MonoBehaviour
{
    // ─── Effettori ────────────────────────────────────────────────────
    public enum Effector { Hand, Tweezers, TweezersGrasp }

    [Header("Effector GameObjects (assegna dall'Inspector)")]
    public GameObject handEffectorGO;
    public GameObject tweezersEffectorGO;
    public GameObject tweezersGraspEffectorGO;

    [Header("Effector tip transforms (end-effector per cinematica)")]
    public Transform handEffectorTip;
    public Transform tweezersEffectorTip;
    public Transform tweezersGraspEffectorTip;

    [Header("CCT Tasks (uno condiviso, swappiamo i riferimenti pollice/indice)")]
    public CCT_Task sharedCCT;

    [Header("CCT Stim Targets — 2 per effettore (sfera luce su pollice + indice)")]
    [Tooltip("Sfera sul pollice della MANO (con misura_manager)")]
    public GameObject stimPollice_Hand;
    [Tooltip("Sfera sull'indice della MANO (con misura_manager)")]
    public GameObject stimIndice_Hand;

    [Tooltip("Sfera sul lato pollice delle TWEEZERS pinch (con misura_manager)")]
    public GameObject stimPollice_Tweezers;
    [Tooltip("Sfera sul lato indice delle TWEEZERS pinch (con misura_manager)")]
    public GameObject stimIndice_Tweezers;

    [Tooltip("Sfera sul lato pollice delle TWEEZERS grasp (con misura_manager)")]
    public GameObject stimPollice_TweezersGrasp;
    [Tooltip("Sfera sul lato indice delle TWEEZERS grasp (con misura_manager)")]
    public GameObject stimIndice_TweezersGrasp;

    [Header("Scene references")]
    public BlackPanelBehaviour blackPanel;
    public CupSwitchDriver2Bases cupDriver;
    public CupEffectorTrigger cupEffectorTrigger;
    public HandKinematicsLogger kinematicsLogger;
    public ExperimentLogger logger; // per Stopwatch sync

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip beepStart;
    public AudioClip beepGo;
    public AudioClip beepReturn;

    // ─── Reach-only parameters ───────────────────────────────────────
    [Header("Reach-only trials")]
    [Tooltip("Numero di reach-only trials per effettore.")]
    public int reachTrialsPerEffector = 8;

    [Tooltip("Durata di un trial reach: dal beep di partenza al beep successivo (s).")]
    public float reachTrialDuration = 4.0f;

    [Tooltip("Gap tra i due beep del doppio beep che annuncia il CCT (s).")]
    public float doubleBeepGap = 0.2f;

    // ─── Counterbalancing ────────────────────────────────────────────
    [Header("Counterbalancing")]
    [Tooltip("Numero soggetto (usato per controbilanciamento ordine effettori). " +
             "Se -1, legge da ParticipantInfo.")]
    public int subjectNumber = -1;

    [Tooltip("Se valorizzato con esattamente 3 effettori, sovrascrive il Latin " +
             "square e i 3 blocchi post-baseline partono in quest'ordine. " +
             "Lasciare vuoto (o lunghezza ≠ 3) per usare il Latin square sul subjectNumber.")]
    public Effector[] manualBlockOrder;

    // ─── Input ───────────────────────────────────────────────────────
    [Header("Input")]
    public KeyCode startKey = KeyCode.T;
    public KeyCode pauseKey = KeyCode.Space;
    [Tooltip("Pre-congela il bicchiere SENZA avviare l'esperimento. Serve per " +
             "permettere al soggetto di posare il braccio sx sul mouse senza che " +
             "il tracking del controller sx trascini il bicchiere virtuale.")]
    public KeyCode freezeCupKey = KeyCode.F;

    // ─── Phase advance (T-gated flow) ────────────────────────────────
    [Header("Phase advance")]
    [Tooltip("If true, every phase transition (baseline-CCT → reach1 → CCT1 → reach2 → ...) waits until the experimenter presses startKey. " +
             "Lets the experimenter position the subject between phases. " +
             "When false, phases run back-to-back (legacy behavior).")]
    public bool advancePhasesWithStartKey = true;

    // ─── State ───────────────────────────────────────────────────────
    [Header("Debug / Stato")]
    [SerializeField] private bool isRunning = false;
    [SerializeField] private bool isPaused = false;
    [SerializeField] private string currentEffectorLabel = "";
    [SerializeField] private int currentReachTrial = 0;

    // ─── Internal ────────────────────────────────────────────────────
    private bool _cupTouched = false;
    private Stopwatch _sw;
    private StreamWriter _logWriter;
    private string _logFilePath;
    private int _resolvedSubjectNum;

    // ─── Latin Square 3×3 completo (6 permutazioni) ─────────────────
    private static readonly Effector[][] LatinSquare = new Effector[][]
    {
        new[] { Effector.Hand,           Effector.Tweezers,      Effector.TweezersGrasp },
        new[] { Effector.Hand,           Effector.TweezersGrasp, Effector.Tweezers      },
        new[] { Effector.Tweezers,       Effector.Hand,          Effector.TweezersGrasp },
        new[] { Effector.Tweezers,       Effector.TweezersGrasp, Effector.Hand          },
        new[] { Effector.TweezersGrasp,  Effector.Hand,          Effector.Tweezers      },
        new[] { Effector.TweezersGrasp,  Effector.Tweezers,      Effector.Hand          },
    };

    // ══════════════════════════════════════════════════════════════════
    // Unity lifecycle
    // ══════════════════════════════════════════════════════════════════

    private void Start()
    {
        // Registra callback tocco dal CupEffectorTrigger
        if (cupEffectorTrigger != null)
            cupEffectorTrigger.onEffectorEnter.AddListener(OnCupTouched);
    }

    private void OnDestroy()
    {
        if (cupEffectorTrigger != null)
            cupEffectorTrigger.onEffectorEnter.RemoveListener(OnCupTouched);

        CloseLog();
    }

    private void Update()
    {
        if (Input.GetKeyDown(startKey) && !isRunning)
        {
            Debug.Log("[CCT_Reach] Avvio esperimento 2 (Reach + CCT)");
            StartCoroutine(RunExperiment());
            return;
        }

        if (isRunning && Input.GetKeyDown(pauseKey))
        {
            TogglePause();
        }

        if (Input.GetKeyDown(freezeCupKey) && cupDriver != null && !cupDriver.IsFrozen)
        {
            cupDriver.SetFrozen(true);
            Debug.Log("[CCT_Reach] Cup pre-frozen — il soggetto può posare il braccio sx sul mouse.");
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Main experiment coroutine
    // ══════════════════════════════════════════════════════════════════

    private IEnumerator RunExperiment()
    {
        isRunning = true;

        // Risolvi numero soggetto
        _resolvedSubjectNum = subjectNumber >= 0
            ? subjectNumber
            : ParseSubjectNumber();

        // Ordine: override manuale da Inspector se valido, altrimenti Latin square
        Effector[] order = (manualBlockOrder != null && manualBlockOrder.Length == 3)
            ? manualBlockOrder
            : LatinSquare[_resolvedSubjectNum % LatinSquare.Length];

        // Init log
        InitLog();
        _sw = (logger != null && logger.sw != null) ? logger.sw : Stopwatch.StartNew();

        LogRow("EXPERIMENT2_START", "",
            $"subj={_resolvedSubjectNum};order={order[0]}_{order[1]}_{order[2]}");

        Debug.Log($"[CCT_Reach] Subject #{_resolvedSubjectNum} — order: {order[0]}, {order[1]}, {order[2]}");

        // Freeze cup tracking per l'intera durata (evita che il bicchiere si muova
        // quando il visore passa a hand tracking sulla sx durante il CCT)
        if (cupDriver != null)
        {
            cupDriver.SetFrozen(true);
            LogRow("CUP_FROZEN", "", "");
        }

        // Pannello disattivato durante CCT (non serve), riattivato solo per il reach.
        if (blackPanel != null) blackPanel.gameObject.SetActive(false);

        // ── Pausa transizione (monta vibromotori) ──
        Debug.Log("[CCT_Reach] Premi T di nuovo o attendi... i motori vibrotattili devono essere montati.");
        yield return new WaitForSeconds(1f);

        // ── 1) BASELINE CCT (mano destra visibile, soggetto risponde col mouse) ──
        // Cup is FROZEN (already set above) so it doesn't move during the CCT.
        Debug.Log("[CCT_Reach] === BASELINE CCT (Hand, no reach) ===");
        ActivateEffector(Effector.Hand);

        // Doppio beep prima del CCT
        yield return StartCoroutine(PlayDoubleBeep());

        LogRow("BASELINE_CCT_START", "Hand", "");

        if (sharedCCT != null)
        {
            sharedCCT.SetCondition("Hand_Baseline_NoReach");
            yield return StartCoroutine(sharedCCT.StartMisura());
        }

        LogRow("BASELINE_CCT_END", "Hand", "");

        // ── 2) Loop 3 blocchi (ordine controbilanciato) ──
        // Il cup resta FROZEN per tutto l'esperimento: lo sperimentatore può
        // togliere il controller fisico dal tavolo senza che il virtuale segua,
        // così durante il reach le dita non compenetrano il bicchiere fisico.
        for (int b = 0; b < order.Length; b++)
        {
            Effector eff = order[b];
            string effLabel = eff.ToString();
            currentEffectorLabel = effLabel;

            // Gate prima dello swap visivo: l'esaminatore legge il questionario
            // embodiment sul blocco precedente (occhi chiusi del soggetto) e solo
            // poi preme startKey per passare al prossimo effettore.
            yield return StartCoroutine(WaitForAdvance(effLabel, $"BLOCK_START_{effLabel}"));

            Debug.Log($"[CCT_Reach] === BLOCCO {b + 1}/3: {effLabel} ===");
            LogRow("BLOCK_START", effLabel, $"blockIdx={b}");

            // Attiva effettore visivo (resta attivo per reach + CCT del blocco)
            ActivateEffector(eff);

            // ─── Gate T → REACH ───
            // L'esaminatore posiziona il soggetto, poi preme T per avviare il reach.
            yield return StartCoroutine(WaitForAdvance(effLabel, "REACH_PHASE_START"));

            // Riattiva e apri il pannello solo per la fase di reach. Il cup resta frozen.
            if (blackPanel != null)
            {
                blackPanel.gameObject.SetActive(true);
                blackPanel.OpenPanel();
            }
            yield return new WaitForSeconds(0.3f);

            LogRow("REACH_PHASE_START", effLabel, $"trials={reachTrialsPerEffector}");

            if (kinematicsLogger != null)
                kinematicsLogger.StartRecording();

            for (int t = 1; t <= reachTrialsPerEffector; t++)
            {
                currentReachTrial = t;
                yield return StartCoroutine(RunReachOnlyTrial(t, effLabel));
            }

            if (kinematicsLogger != null)
                kinematicsLogger.StopRecording();

            LogRow("REACH_PHASE_END", effLabel, "");

            // Chiudi e disattiva il pannello al termine del reach.
            if (blackPanel != null)
            {
                blackPanel.ClosePanel();
                blackPanel.gameObject.SetActive(false);
            }

            // Doppio beep di fine reach
            yield return StartCoroutine(PlayDoubleBeep());

            // ─── Gate T → CCT ───
            yield return StartCoroutine(WaitForAdvance(effLabel, "CCT_START"));

            LogRow("CCT_START", effLabel, "");

            if (sharedCCT != null)
            {
                sharedCCT.SetCondition($"{effLabel}_PostReach");
                yield return StartCoroutine(sharedCCT.StartMisura());
            }

            LogRow("CCT_END", effLabel, "");
            LogRow("BLOCK_END", effLabel, $"blockIdx={b}");
        }

        // ── Fine ──
        if (blackPanel != null)
        {
            blackPanel.ClosePanel();
            blackPanel.gameObject.SetActive(false);
        }
        if (cupDriver != null)
        {
            cupDriver.SetFrozen(false);
            cupDriver.pickupDisabled = false;
        }

        LogRow("EXPERIMENT2_END", "", "");
        CloseLog();
        isRunning = false;
        currentEffectorLabel = "";
        Debug.Log("[CCT_Reach] Esperimento 2 terminato.");
    }

    private IEnumerator PlayDoubleBeep()
    {
        PlayBeep(beepReturn);
        yield return new WaitForSeconds(doubleBeepGap);
        PlayBeep(beepReturn);
    }

    /// <summary>
    /// Block until the experimenter presses startKey. Logs WAIT_FOR_ADVANCE / ADVANCE_PRESSED
    /// so phase boundaries are visible in the CSV. No-op if advancePhasesWithStartKey is false.
    /// </summary>
    private IEnumerator WaitForAdvance(string effLabel, string nextPhase)
    {
        if (!advancePhasesWithStartKey) yield break;
        Debug.Log($"[CCT_Reach] Premi {startKey} per iniziare: {nextPhase}");
        LogRow("WAIT_FOR_ADVANCE", effLabel, $"next={nextPhase}");
        yield return null; // skip the frame where startKey was pressed to enter this phase
        while (!Input.GetKeyDown(startKey)) yield return null;
        LogRow("ADVANCE_PRESSED", effLabel, $"next={nextPhase}");
    }

    // ══════════════════════════════════════════════════════════════════
    // Reach-only trial
    // ══════════════════════════════════════════════════════════════════

    private IEnumerator RunReachOnlyTrial(int trialIdx, string effLabel)
    {
        _cupTouched = false;

        if (kinematicsLogger != null)
            kinematicsLogger.SetTrialInfo(trialIdx, "Reaching");

        // Beep di partenza
        PlayBeep(beepGo);
        long goMs = NowMs();
        LogRow("REACH_TRIAL_START", effLabel, $"trial={trialIdx};sw_ms={goMs}");

        // Finestra unica di reachTrialDuration: il soggetto tocca e torna sul tavolo.
        bool hit = false;
        long reachRT = -1;
        float elapsed = 0f;
        while (elapsed < reachTrialDuration)
        {
            if (!hit && _cupTouched)
            {
                hit = true;
                long touchMs = NowMs();
                reachRT = touchMs - goMs;
                LogRow("REACH_TOUCH", effLabel,
                    $"trial={trialIdx};sw_ms={touchMs};RT_ms={reachRT}");

                if (kinematicsLogger != null)
                    kinematicsLogger.SetTrialInfo(trialIdx, "Touched");
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!hit)
        {
            LogRow("REACH_MISS", effLabel,
                $"trial={trialIdx};duration={reachTrialDuration}");

            if (kinematicsLogger != null)
                kinematicsLogger.SetTrialInfo(trialIdx, "Miss");
        }

        LogRow("REACH_TRIAL_END", effLabel,
            $"trial={trialIdx};hit={hit};RT_ms={reachRT}");

        if (kinematicsLogger != null)
            kinematicsLogger.SetTrialInfo(0, "None");

        _cupTouched = false;
    }

    // ══════════════════════════════════════════════════════════════════
    // Effector activation
    // ══════════════════════════════════════════════════════════════════

    private void ActivateEffector(Effector eff)
    {
        if (handEffectorGO != null)          handEffectorGO.SetActive(eff == Effector.Hand);
        if (tweezersEffectorGO != null)      tweezersEffectorGO.SetActive(eff == Effector.Tweezers);
        if (tweezersGraspEffectorGO != null) tweezersGraspEffectorGO.SetActive(eff == Effector.TweezersGrasp);

        // Aggiorna la tool tip del logger cinematico (la hand resta fissa per coerenza tra effettori)
        if (kinematicsLogger != null)
        {
            Transform toolTip = eff switch
            {
                Effector.Tweezers      => tweezersEffectorTip,
                Effector.TweezersGrasp => tweezersGraspEffectorTip,
                _                      => handEffectorTip,
            };
            kinematicsLogger.SetToolTipTransform(toolTip);
        }

        // Swappa i riferimenti pollice/indice nel CCT_Task condiviso
        SwapCCTStimTargets(eff);

        Debug.Log($"[CCT_Reach] Effettore attivo: {eff}");
    }

    /// <summary>
    /// Assegna al CCT_Task condiviso le sfere stim corrette per l'effettore attivo
    /// e gestisce la visibilità.
    /// </summary>
    private void SwapCCTStimTargets(Effector eff)
    {
        if (sharedCCT == null) return;

        switch (eff)
        {
            case Effector.Hand:
                sharedCCT.pollice = stimPollice_Hand;
                sharedCCT.indice  = stimIndice_Hand;
                break;
            case Effector.Tweezers:
                sharedCCT.pollice = stimPollice_Tweezers;
                sharedCCT.indice  = stimIndice_Tweezers;
                break;
            case Effector.TweezersGrasp:
                sharedCCT.pollice = stimPollice_TweezersGrasp;
                sharedCCT.indice  = stimIndice_TweezersGrasp;
                break;
        }

        // Aggiorna anche i misura_manager (verranno ri-letti da StartMisura)
        sharedCCT.misura_pollice = sharedCCT.pollice != null
            ? sharedCCT.pollice.GetComponent<misura_manager>() : null;
        sharedCCT.misura_indice = sharedCCT.indice != null
            ? sharedCCT.indice.GetComponent<misura_manager>() : null;

        UpdateStimVisibility(eff);

        Debug.Log($"[CCT_Reach] Stim targets swappati → pollice={sharedCCT.pollice?.name}, indice={sharedCCT.indice?.name}");
    }

    /// <summary>
    /// Unico owner della visibilità delle 6 palline stim: spegne tutte e accende
    /// solo le 2 dell'effettore indicato. Chiamabile anche dall'esterno
    /// (es. ReachGraspGameManager quando cicla effettore con E).
    /// </summary>
    public void UpdateStimVisibility(Effector eff)
    {
        SetStimVisible(stimPollice_Hand, eff == Effector.Hand);
        SetStimVisible(stimIndice_Hand,  eff == Effector.Hand);
        SetStimVisible(stimPollice_Tweezers, eff == Effector.Tweezers);
        SetStimVisible(stimIndice_Tweezers,  eff == Effector.Tweezers);
        SetStimVisible(stimPollice_TweezersGrasp, eff == Effector.TweezersGrasp);
        SetStimVisible(stimIndice_TweezersGrasp,  eff == Effector.TweezersGrasp);
    }

    private static void SetStimVisible(GameObject go, bool visible)
    {
        if (go == null) return;
        if (!go.activeSelf) go.SetActive(true);
        var r = go.GetComponent<Renderer>();
        if (r != null) r.enabled = visible;
    }

    // ══════════════════════════════════════════════════════════════════
    // Touch callback (dal CupEffectorTrigger UnityEvent)
    // ══════════════════════════════════════════════════════════════════

    public void OnCupTouched()
    {
        _cupTouched = true;
    }

    // ══════════════════════════════════════════════════════════════════
    // Pause
    // ══════════════════════════════════════════════════════════════════

    private void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        if (audioSource != null)
        {
            if (isPaused) audioSource.Pause(); else audioSource.UnPause();
        }
        LogRow(isPaused ? "PAUSED" : "RESUMED", currentEffectorLabel, "");
        Debug.Log($"[CCT_Reach] {(isPaused ? "PAUSA" : "RIPRESO")}");
    }

    // ══════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════

    private void PlayBeep(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private long NowMs()
    {
        return _sw != null ? _sw.ElapsedMilliseconds : (long)(Time.time * 1000);
    }

    private int ParseSubjectNumber()
    {
        var pi = ParticipantInfo.Instance;
        if (pi != null && int.TryParse(pi.NumeroSoggetto, out int n))
            return n;
        return 0;
    }

    // ══════════════════════════════════════════════════════════════════
    // Logging (CSV dedicato per reach trials)
    // ══════════════════════════════════════════════════════════════════

    private void InitLog()
    {
        string dir;
        if (logger != null && !string.IsNullOrEmpty(logger.FilePath))
            dir = Path.GetDirectoryName(logger.FilePath);
        else
            dir = Path.Combine(Application.persistentDataPath, "Logs");

        Directory.CreateDirectory(dir);

        string subj = logger != null ? logger.subjectId : "unknown";
        string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _logFilePath = Path.Combine(dir, $"CCT_Reach_{subj}_{ts}.csv");

        _logWriter = new StreamWriter(_logFilePath, false, new UTF8Encoding(false));
        _logWriter.WriteLine("pc_time,sw_ms,frame,event,effector,detail");
        _logWriter.Flush();

        Debug.Log($"[CCT_Reach] Log → {_logFilePath}");
    }

    private void LogRow(string eventName, string effector, string detail)
    {
        if (_logWriter == null) return;

        var ci = CultureInfo.InvariantCulture;
        string line = string.Join(",",
            $"\"{DateTime.Now.ToString("o", ci)}\"",
            NowMs().ToString(ci),
            Time.frameCount.ToString(ci),
            $"\"{eventName}\"",
            $"\"{effector}\"",
            $"\"{detail}\""
        );

        _logWriter.WriteLine(line);
        _logWriter.Flush();
    }

    private void CloseLog()
    {
        if (_logWriter != null)
        {
            _logWriter.Flush();
            _logWriter.Close();
            _logWriter = null;
        }
    }
}

