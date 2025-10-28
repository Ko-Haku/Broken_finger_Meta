using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using TMPro;
using Debug = UnityEngine.Debug;

public class ExperimentRT : MonoBehaviour
{
    // === TIPI CONDIZIONI ===
    public enum Modality { Hand, Pinza }
    public enum Stretch { Baseline, Stretch1, Stretch2 } // tre livelli di “escursione all’indietro”

    [System.Serializable]
    public struct BlockPlan
    {
        public EffectorRig rig;     // Hand rig o Pinza rig
        public Modality modality;   // Hand / Pinza
        public Stretch stretch;     // Baseline / Stretch1 / Stretch2
        public int trials;          // numero di trial nel blocco
    }

    [Header("AutoPlan")]
    public bool autoPlan = true;
    public EffectorRig handRig;
    public EffectorRig pinzaRig;
    public int trialsPerBlock = 12;     // # trial per blocco
    public bool handFirst = true;       // ordine macro tra Hand e Pinza

    [Header("Piano sperimentale (in ordine)")]
    public BlockPlan[] plan;            // se vuoi fissare un piano manuale, compila qui

    [Header("Prefab pallina (Burst)")]
    public GameObject ballPrefab;       // prefab con collider isTrigger + BurstBall

    [Header("UI feedback")]
    public TextMeshProUGUI feedbackText;      // mostra “Bolle: X/10”
    public bool showOpenAmountOnUI = false;   // utile per debug

    [Header("Timing")]
    public float trialDuration = 3.0f;        // durata di ogni trial (secondi)
    public float interTrialInterval = 1.0f;
    public float readyTime = 0.6f;
    public float startPoseTimeoutSec = 6f;    // attesa partenza in pinch

    [Header("Gating pinch")]
    [Range(0,1)] public float pinchMax = 0.25f; // pinch = openAmount <= pinchMax

    [Header("Parametri Mano (PinchOpenDriver)")]
    public float handBaseline_dMin = 0.002f;
    public float handBaseline_dMax = 0.200f;
    public float handStretch1_dMin = 0.020f;
    public float handStretch1_dMax = 0.040f;
    public float handStretch2_dMin = 0.030f;
    public float handStretch2_dMax = 0.050f;

    // === Parametri Pinza (ANIMATOR via PinchOpenDriver_Interaction) ===
    [Header("Parametri Pinza (Animator driver)")]
    public float pinzaBaseline_dMin = 0.020f;
    public float pinzaBaseline_dMax = 0.080f;
    public float pinzaStretch1_dMin = 0.020f;
    public float pinzaStretch1_dMax = 0.060f;
    public float pinzaStretch2_dMin = 0.020f;
    public float pinzaStretch2_dMax = 0.040f;

    public GripperController pinzaController; // opzionale: non più usato se vai via Animator

    [Header("Semi-arco palline")]
    public int ballsPerTrial = 10;
    public float arcRadius = 0.10f;       // raggio semiarco
    [Range(0,180)] public float arcSpanDeg = 75f; // ampiezza semiarco (0..180)
    public Vector3 arcAxisLocal = Vector3.right;  // ruota attorno a wrist.right (backward pitch)
    public float arcVerticalOffset = 0.0f;        // piccolo offset verticale se serve

    [Header("Dark screen / CCT")]
    public GameObject darkScreen;
    public float darkScreenTime = 2f;
    public float cctStartDelay = 1f;

    [Header("Preview all’avvio")]
    public bool showFirstRigOnStart = true;
    public bool forceDeactivateOtherRigs = true;
    public float pauseBeforeFirstCCT = 0f;
    private bool experimentRunning = false;   // guardia anti-doppio avvio

    [Header("Questionario / Inter-blocco")]
    [Tooltip("Mostra la dark screen tra il questionario del blocco N e il burst del blocco N+1 (copre il cambio parametri).")]
    public float postQuestionnaireDarkSec = 1.0f;
   // public float blackoutAfterQuestionnaire = 1f;
    // Dati soggetto
    private string codiceSoggetto = "NA", condizione = "NA", numeroSoggetto = "NA";

    // Internals
    private Stopwatch sw = new Stopwatch();
    private List<string> log = new List<string>();
    private int currentBlockIndex = -1, currentTrial;
    private int poppedThisTrial;
    private List<GameObject> spawnedBalls = new List<GameObject>();

    // === Helpers ===
    string ConditionLabel(BlockPlan b) => $"{b.modality}_{b.stretch}";

    IEnumerator WaitForQuestionnaireKey()
    {
        if (feedbackText) feedbackText.text = "Questionario: premi R per continuare";
        yield return null; // debounce
        while (!Input.GetKeyDown(KeyCode.R))
            yield return null;
        if (feedbackText) feedbackText.text = "";
    }

    void Start()
    {
        var pi = ParticipantInfo.Instance;
        if (pi != null)
        {
            codiceSoggetto = string.IsNullOrEmpty(pi.Codice_Soggetto) ? "NA" : pi.Codice_Soggetto;
            condizione     = string.IsNullOrEmpty(pi.Condizione)       ? "NA" : pi.Condizione;
            numeroSoggetto = string.IsNullOrEmpty(pi.NumeroSoggetto)   ? "NA" : pi.NumeroSoggetto;
        }

        log.Clear();
        log.Add("CodiceSoggetto,NumeroSoggetto,Condizione,BlockIdx,Trial,Modality,Stretch,BallsPopped,TrialDuration_s");

        if (autoPlan) BuildAutoPlan();

        // nascondi tutti i rig (ibrido)
        if (plan != null)
            foreach (var b in plan)
                if (b.rig != null) ShowRigHybrid(b.rig, false);

        // preview primo rig
        if (showFirstRigOnStart && plan != null && plan.Length > 0 && plan[0].rig != null)
        {
            ActivateOnlyHybrid(plan[0].rig);
            ApplyConditionParameters(plan[0]); // parametri visibili già in preview
        }

        if (feedbackText) feedbackText.text = "Premi S per iniziare";

        // dark all'avvio
        if (darkScreen) darkScreen.SetActive(true);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S) && !experimentRunning)
        {
            if (darkScreen) darkScreen.SetActive(false);
            experimentRunning = true;
            StartCoroutine(RunExperiment());
        }

        if (showOpenAmountOnUI && feedbackText)
        {
            var rig = GetActiveRig();
            if (rig != null && rig.pinchDriver != null)
                feedbackText.text = $"OpenAmount: {rig.pinchDriver.openAmount:0.00}";
        }
    }

    EffectorRig GetActiveRig()
    {
        return (currentBlockIndex >= 0 && plan != null && currentBlockIndex < plan.Length)
            ? plan[currentBlockIndex].rig
            : (plan != null && plan.Length > 0 ? plan[0].rig : null);
    }

    // === HYBRID SHOW/HIDE ===
    void ShowRigHybrid(EffectorRig rig, bool on)
    {
        if (rig == null) return;

        if (rig.rigType == EffectorRig.RigType.Pinza)
        {
            rig.gameObject.SetActive(on);        // pinza: ON/OFF intero GO
        }
        else
        {
            rig.ShowBothHandVariants(on);        // mano: mostrale entrambe
            rig.ShowRig(on);                     // abilita renderer/collider
        }
    }

    void ActivateOnlyHybrid(EffectorRig rigToShow)
    {
        if (plan == null) return;
        foreach (var b in plan)
        {
            if (b.rig == null) continue;
            ShowRigHybrid(b.rig, b.rig == rigToShow);
        }
    }
// 1) helper in ExperimentRT
    IEnumerator Blackout(float seconds)
    {
        if (!darkScreen || seconds <= 0f) yield break;
        darkScreen.SetActive(true);
        yield return new WaitForSeconds(seconds);
        darkScreen.SetActive(false);
    }

   IEnumerator RunExperiment()
   {
       if (plan == null || plan.Length == 0)
       {
           Debug.LogWarning("RunExperiment: plan vuoto.");
           yield break;
       }
   
       // Disattiva tutti i rig all’inizio
       if (forceDeactivateOtherRigs)
           foreach (var b in plan) if (b.rig) ShowRigHybrid(b.rig, false);
   
       // ===== CCT BASELINE INIZIALE (sul primo rig) =====
       var firstBlock = plan[0];
       var firstRig   = firstBlock.rig;
       if (firstRig != null)
       {
           // mostra SOLO il primo rig
           if (forceDeactivateOtherRigs) ActivateOnlyHybrid(firstRig);
           else ShowRigHybrid(firstRig, true);
   
           // etichetta condizione nel CCT (es. "Hand_Baseline_CCT_Initial" / "Pinza_Baseline_CCT_Initial")
           if (firstRig.cctTask != null)
               firstRig.cctTask.SetCondition($"{firstBlock.modality}_{firstBlock.stretch}_CCT_Initial");
   
           // Applica parametri visivi della condizione corrente
           ApplyConditionParameters(firstBlock);
   
           // Lancia CCT (RunCCT forza temporaneamente la baseline dei parametri, come da tua logica)
           yield return StartCoroutine(RunCCT(firstRig));
   
           // Questionario
           yield return StartCoroutine(WaitForQuestionnaireKey());
           // dopo il questionario, SEMPRE un blackout breve prima del burst del blocco successivo
           yield return StartCoroutine(Blackout(postQuestionnaireDarkSec));

   
          
       }
   
       // ===== LOOP BLOCCHI =====
       for (currentBlockIndex = 0; currentBlockIndex < plan.Length; currentBlockIndex++)
       {
           var block = plan[currentBlockIndex];
           if (block.rig == null) continue;
   
           // mostra SOLO il rig del blocco
           if (forceDeactivateOtherRigs) ActivateOnlyHybrid(block.rig);
           else ShowRigHybrid(block.rig, true);
   
           // Se stiamo entrando in una NUOVA MODALITÀ visiva rispetto al blocco precedente,
           // fai un CCT baseline PRIMA di partire col burst di questo blocco.
           bool modalityChanged = false;
           if (currentBlockIndex > 0)
           {
               var prev = plan[currentBlockIndex - 1];
               modalityChanged = (prev.modality != block.modality);
           }
   
           if (modalityChanged)
           {
               // etichetta per chiarezza nel file CCT
               if (block.rig.cctTask != null)
                   block.rig.cctTask.SetCondition($"{block.modality}_{block.stretch}_CCT_PreSwitch");
   
               // assesta i parametri visivi per questa condizione
               ApplyConditionParameters(block);
   
               // CCT baseline prima del nuovo tipo di visual
               yield return StartCoroutine(RunCCT(block.rig));
   
               // Questionario
               yield return StartCoroutine(WaitForQuestionnaireKey());
   
               // Blackout breve per evitare di far vedere lo switch dei parametri
               yield return StartCoroutine(Blackout(postQuestionnaireDarkSec));
           }
           else
           {
               // comunque assesta i parametri visivi della condizione (se non li hai già assestati sopra)
               ApplyConditionParameters(block);
           }
   
           // ===== TRIALS (BURST) =====
           for (currentTrial = 0; currentTrial < Mathf.Max(1, block.trials); currentTrial++)
           {
               // Gating: partenza in pinch
               yield return StartCoroutine(WaitForPinch(block.rig));
               if (feedbackText) feedbackText.text = "READY";
               yield return new WaitForSeconds(readyTime);
   
               // Spawn palline in semiarco
               SpawnBallsArc(block.rig);
   
               poppedThisTrial = 0;
               UpdateFeedback();
   
               // finestra temporale
               sw.Restart();
               float t = 0f;
               while (t < trialDuration)
               {
                   t += Time.deltaTime;
                   yield return null;
               }
               sw.Stop();
   
               // cleanup
               CleanupBalls();
               if (feedbackText) UpdateFeedback();
               yield return new WaitForSeconds(interTrialInterval);
   
               // log
               LogRow(block, poppedThisTrial, trialDuration);
           }
   
           // ===== CCT POST-BLOCCO (come prima) =====
           if (block.rig.cctTask != null)
               block.rig.cctTask.SetCondition($"{block.modality}_{block.stretch}_CCT_Post");
   
           yield return StartCoroutine(RunCCT(block.rig));
   
           // Questionario
           yield return StartCoroutine(WaitForQuestionnaireKey());
           yield return StartCoroutine(Blackout(postQuestionnaireDarkSec));
           // spegni rig finito
           ShowRigHybrid(block.rig, false);
           SaveLog();
       }
   
   #if UNITY_EDITOR
       UnityEditor.EditorApplication.isPlaying = false;
   #else
       Application.Quit();
   #endif
   }


    void ApplyConditionParameters(BlockPlan block)
    {
        // Prendiamo SEMPRE un PinchOpenDriver_Interaction dal rig corrente:
        var drv = block.rig.pinchDriver;
        if (drv == null) drv = block.rig.GetComponentInChildren<PinchOpenDriver_Interaction>(true);

        if (block.modality == Modality.Hand)
        {
            // Nascondi l’indice XR perché mostri il dito “masked”
            if (block.rig.indexHider) block.rig.indexHider.SetVisible(false);

            if (drv != null)
            {
                switch (block.stretch)
                {
                    default:
                    case Stretch.Baseline: TrySetDriverRange(drv, handBaseline_dMin, handBaseline_dMax); break;
                    case Stretch.Stretch1: TrySetDriverRange(drv, handStretch1_dMin, handStretch1_dMax); break;
                    case Stretch.Stretch2: TrySetDriverRange(drv, handStretch2_dMin, handStretch2_dMax); break;
                }
            }
            else
            {
                Debug.LogWarning("ApplyConditionParameters: PinchOpenDriver_Interaction non trovato sul rig MANO.");
            }
        }
        else // === PINZA ===
        {
            // Per la pinza ci serve l’indice XR visibile (serve la distanza reale delle dita)
            if (block.rig.indexHider) block.rig.indexHider.SetVisible(true);

            if (drv != null)
            {
                // Mappiamo i tre livelli su dMin/dMax della pinza (che guidano Animator 'Open')
                switch (block.stretch)
                {
                    default:
                    case Stretch.Baseline: TrySetDriverRange(drv, pinzaBaseline_dMin, pinzaBaseline_dMax); break;
                    case Stretch.Stretch1: TrySetDriverRange(drv, pinzaStretch1_dMin, pinzaStretch1_dMax); break;
                    case Stretch.Stretch2: TrySetDriverRange(drv, pinzaStretch2_dMin, pinzaStretch2_dMax); break;
                }
            }
            else
            {
                Debug.LogWarning("ApplyConditionParameters: PinchOpenDriver_Interaction non trovato sul rig PINZA.");
            }
        }
    }

    void TrySetDriverRange(PinchOpenDriver_Interaction drv, float dMin, float dMax)
    {
        drv.dMin = dMin;
        drv.dMax = dMax;
    }

    IEnumerator WaitForPinch(EffectorRig rig)
    {
        float elapsed = 0f;
        if (rig.pinchDriver == null) yield break;

        if (feedbackText && !showOpenAmountOnUI) feedbackText.text = "Postura: PINCH";
        while (elapsed < startPoseTimeoutSec && rig.pinchDriver.openAmount > pinchMax)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    void SpawnBallsArc(EffectorRig rig)
    {
        CleanupBalls();
        if (!ballPrefab) { Debug.LogWarning("ballPrefab non assegnato."); return; }
        if (!rig) { Debug.LogWarning("SpawnBallsArc: rig nullo."); return; }

        // Anchor = reference HELPFUL (fallback finger/wrist)
        Transform anchor = rig.helpfulTargetRef ? rig.helpfulTargetRef
            : (rig.fingerSpawnPoint ? rig.fingerSpawnPoint : rig.wrist);
        if (!anchor) { Debug.LogWarning("SpawnBallsArc: nessun anchor valido."); return; }

        Vector3 up   = anchor.up;
        Vector3 back = (anchor.forward).normalized; // indietro
        Vector3 down = (-up).normalized;            // verso il basso

        Vector3 v1 = back;
        Vector3 v2 = (down - Vector3.Dot(down, v1) * v1).normalized; // down-in-plane
        Vector3 center = anchor.position + up * arcVerticalOffset - arcRadius * v1;

        float step = (ballsPerTrial > 1) ? (Mathf.Deg2Rad * arcSpanDeg / (ballsPerTrial - 1)) : 0f;

        for (int i = 0; i < ballsPerTrial; i++)
        {
            float theta = -step * i; // negativo → verso destra

            Vector3 dir = (Mathf.Cos(theta) * v1 + Mathf.Sin(theta) * v2).normalized; // indietro → destra → giù
            Vector3 pos = center + dir * arcRadius;
            Quaternion rot = Quaternion.LookRotation(-dir, up);

            var ball = Instantiate(ballPrefab, pos, rot, anchor);
            var bb = ball.GetComponent<BurstBall>(); if (!bb) bb = ball.AddComponent<BurstBall>();
            bb.manager = this;

            spawnedBalls.Add(ball);
        }
    }

    void CleanupBalls()
    {
        for (int i = 0; i < spawnedBalls.Count; i++)
        {
            if (spawnedBalls[i]) Destroy(spawnedBalls[i]);
        }
        spawnedBalls.Clear();
    }

    public void OnBallPopped(GameObject ball)
    {
        poppedThisTrial++;
        if (ball) { spawnedBalls.Remove(ball); Destroy(ball); }
        UpdateFeedback();
    }

    void UpdateFeedback()
    {
        if (!feedbackText) return;
        feedbackText.text = $"Bolle: {poppedThisTrial}/{ballsPerTrial}";
    }

    // --- Snapshot driver per CCT baseline ---
    struct DriverSnapshot {
        public PinchOpenDriver_Interaction drv;
        public float dMin, dMax;
        public bool hasDriver;
    }

    DriverSnapshot ForceBaselineForCCT(EffectorRig rig)
    {
        var snap = new DriverSnapshot { drv = null, hasDriver = false, dMin = 0f, dMax = 0f };
        if (rig == null) return snap;

        var drv = rig.pinchDriver;
        if (drv == null) drv = rig.GetComponentInChildren<PinchOpenDriver_Interaction>(true);
        if (drv == null) return snap;

        snap.drv = drv;
        snap.hasDriver = true;
        snap.dMin = drv.dMin;
        snap.dMax = drv.dMax;

        // baseline per Mano o Pinza
        if (rig.rigType == EffectorRig.RigType.Hand)
        {
            drv.dMin = handBaseline_dMin;
            drv.dMax = handBaseline_dMax;
            if (rig.indexHider) rig.indexHider.SetVisible(false);
        }
        else // Pinza
        {
            drv.dMin = pinzaBaseline_dMin;
            drv.dMax = pinzaBaseline_dMax;
            if (rig.indexHider) rig.indexHider.SetVisible(true);
        }

        return snap;
    }

    void RestoreAfterCCT(DriverSnapshot snap)
    {
        if (!snap.hasDriver || snap.drv == null) return;
        snap.drv.dMin = snap.dMin;
        snap.drv.dMax = snap.dMax;
    }

    IEnumerator RunCCT(EffectorRig rig)
    {
        if (rig == null || rig.cctTask == null) yield break;

        // Forza temporaneamente la baseline per il CCT
        var snap = ForceBaselineForCCT(rig);

        // etichetta condizione nel file del CCT (es. "Hand_BaselineCCT" / "Pinza_BaselineCCT")
       // string cctCond = (rig.rigType == EffectorRig.RigType.Hand) ? "Hand_BaselineCCT" : "Pinza_BaselineCCT";
        string cctCond;
		// Se siamo dentro un blocco valido, usa la condizione di provenienza
	if (currentBlockIndex >= 0 && plan != null && currentBlockIndex < plan.Length)
		{		
   			 var b = plan[currentBlockIndex];
   			 cctCond = $"{b.modality}_{b.stretch}_CCT";
		}
	else
		{
    			// Altrimenti (CCT iniziale pre-esperimento)
   			 cctCond = (rig.rigType == EffectorRig.RigType.Hand)
      		  ? "Hand_BaselineCCT"
       		 : "Pinza_BaselineCCT";
		}



		rig.cctTask.SetCondition(cctCond);

        if (rig.rigType == EffectorRig.RigType.Hand)
            rig.ShowBothHandVariants(true);

        if (darkScreen) darkScreen.SetActive(true);
        yield return new WaitForSeconds(darkScreenTime);
        if (darkScreen) darkScreen.SetActive(false);

        if (cctStartDelay > 0f)
            yield return new WaitForSeconds(cctStartDelay);

        yield return StartCoroutine(rig.cctTask.StartMisura());

        // Ripristina i parametri del driver dopo il CCT
        RestoreAfterCCT(snap);

        SaveLog();
    }

    void LogRow(BlockPlan b, int ballsPopped, float durSec)
    {
        string modality = (b.modality == Modality.Hand) ? "Hand" : "Pinza";
        string row = $"{codiceSoggetto},{numeroSoggetto},{condizione}," +
                     $"{currentBlockIndex+1},{currentTrial+1},{modality},{b.stretch},{ballsPopped},{durSec:0.00}";
        log.Add(row);
        SaveLog();
    }

    void SaveLog()
    {
        string dir = Path.Combine(Application.dataPath, "Reports");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"BURST_{codiceSoggetto}_{condizione}.csv");
        File.WriteAllLines(path, log);
    }

    // === AUTOPLAN: Hand(Baseline→Stretch1→Stretch2) poi Pinza (o viceversa) ===
    void BuildAutoPlan()
    {
        if (handRig == null || pinzaRig == null)
        {
            Debug.LogWarning("AutoPlan: assegna handRig e pinzaRig.");
            return;
        }
        if (plan != null && plan.Length > 0) return;

        var handBlocks = new List<BlockPlan>
        {
            new BlockPlan{ rig = handRig, modality = Modality.Hand,  stretch = Stretch.Baseline, trials = trialsPerBlock },
            new BlockPlan{ rig = handRig, modality = Modality.Hand,  stretch = Stretch.Stretch1, trials = trialsPerBlock },
            new BlockPlan{ rig = handRig, modality = Modality.Hand,  stretch = Stretch.Stretch2, trials = trialsPerBlock },
        };
        var pinzaBlocks = new List<BlockPlan>
        {
            new BlockPlan{ rig = pinzaRig, modality = Modality.Pinza, stretch = Stretch.Baseline, trials = trialsPerBlock },
            new BlockPlan{ rig = pinzaRig, modality = Modality.Pinza, stretch = Stretch.Stretch1, trials = trialsPerBlock },
            new BlockPlan{ rig = pinzaRig, modality = Modality.Pinza, stretch = Stretch.Stretch2, trials = trialsPerBlock },
        };

        var final = new List<BlockPlan>(6);
        if (handFirst) { final.AddRange(handBlocks); final.AddRange(pinzaBlocks); }
        else           { final.AddRange(pinzaBlocks); final.AddRange(handBlocks); }

        plan = final.ToArray();
    }
}
