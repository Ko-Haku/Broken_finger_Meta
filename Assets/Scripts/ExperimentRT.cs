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
    public int trialsPerBlock = 5;     // # trial per blocco
    public bool handFirst = true;      // ordine macro tra Hand e Pinza

    [Header("Piano sperimentale (in ordine)")]
    public BlockPlan[] plan;           // se vuoi fissare un piano manuale, compila qui

    [Header("Prefab pallina (Burst)")]
    public GameObject ballPrefab;      // prefab con collider isTrigger + BurstBall

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
    // Valori richiesti dal PI per le tre condizioni (manipoliamo dMin/dMax del driver)
    public float handBaseline_dMin = 0.002f;
    public float handBaseline_dMax = 0.200f;
    public float handStretch1_dMin = 0.020f;
    public float handStretch1_dMax = 0.040f;
    public float handStretch2_dMin = 0.030f;
    public float handStretch2_dMax = 0.050f;

    [Header("Parametri Pinza (Gripper)")]
    // Max angle per le tre condizioni
    public float pinzaBaseline_maxDeg = 90f;
    public float pinzaStretch1_maxDeg = 120f;
    public float pinzaStretch2_maxDeg = 150f;
    public GripperController pinzaController; // opzionale: script con proprietà MaxAngleDeg

    [Header("Semi-arco palline")]
    public int ballsPerTrial = 10;
    public float arcRadius = 0.10f;       // raggio semiarco
    [Range(0,180)] public float arcSpanDeg = 75f; // ampiezza semiarco (0..180)
    public Vector3 arcAxisLocal = Vector3.right;  // ruota attorno a wrist.right (backward pitch)
    public float arcVerticalOffset = 0.0f;        // piccolo offset verticale se serve

    [Header("Dark screen / CCT")]
    public GameObject darkScreen;
    public float darkScreenTime = 2f;

    [Header("Preview all’avvio")]
    public bool showFirstRigOnStart = true;
    public bool forceDeactivateOtherRigs = true;
    public float pauseBeforeFirstCCT = 0f;

    // Dati soggetto
    private string codiceSoggetto = "NA", condizione = "NA", numeroSoggetto = "NA";

    // Internals
    private Stopwatch sw = new Stopwatch();
    private List<string> log = new List<string>();
    private int currentBlockIndex = -1, currentTrial;
    private int poppedThisTrial;
    private List<GameObject> spawnedBalls = new List<GameObject>();

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
            ActivateOnlyHybrid(plan[0].rig);

        if (feedbackText) feedbackText.text = "Premi S per iniziare";
        // preview primo rig
        if (showFirstRigOnStart && plan != null && plan.Length > 0 && plan[0].rig != null)
        {
            ActivateOnlyHybrid(plan[0].rig);
            // NEW: applica i parametri del primo blocco già in preview
            ApplyConditionParameters(plan[0]);
        }

    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
            StartCoroutine(RunExperiment());

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

    IEnumerator RunExperiment()
    {
        

        if (plan == null || plan.Length == 0)
        {
            Debug.LogWarning("RunExperiment: plan vuoto.");
            yield break;
        }

        // CCT baseline UNA VOLTA all’inizio (sul primo rig)
        var firstRig = plan[0].rig;
        if (firstRig != null)
        {
            if (forceDeactivateOtherRigs) ActivateOnlyHybrid(firstRig);
            else ShowRigHybrid(firstRig, true);

            if (pauseBeforeFirstCCT > 0f)
                yield return new WaitForSeconds(pauseBeforeFirstCCT);
            ApplyConditionParameters(plan[0]);
            yield return StartCoroutine(RunCCT(firstRig));
        }

        // Loop blocchi
        for (currentBlockIndex = 0; currentBlockIndex < plan.Length; currentBlockIndex++)
        {
            var block = plan[currentBlockIndex];
            if (block.rig == null) continue;

            if (forceDeactivateOtherRigs) ActivateOnlyHybrid(block.rig);
            else ShowRigHybrid(block.rig, true);

            // Applica parametri di condizione (mano/pinza)
            ApplyConditionParameters(block);

            // Trial loop
            for (currentTrial = 0; currentTrial < Mathf.Max(1, block.trials); currentTrial++)
            {
                // Gating: partenza in PINCH
                yield return StartCoroutine(WaitForPinch(block.rig));
                if (feedbackText) feedbackText.text = "READY";
                yield return new WaitForSeconds(readyTime);

                // Spawn 10 palline in semiarco
                SpawnBallsArc(block.rig);

                poppedThisTrial = 0;
                UpdateFeedback();

                // finestra temporale per “scoppiare quante più palline”
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
                if (feedbackText) UpdateFeedback(); // mostra punteggio finale del trial
                yield return new WaitForSeconds(interTrialInterval);

                // log
                LogRow(block, poppedThisTrial, trialDuration);
            }

            // CCT post-blocco
            yield return StartCoroutine(RunCCT(block.rig));

            // spegni rig (ibrido)
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
        if (block.modality == Modality.Hand)
        {
            // Nascondi XR Index
            if (block.rig.indexHider) block.rig.indexHider.SetVisible(false);

            var drv = block.rig.pinchDriver;
            if (drv != null)
            {
                switch (block.stretch)
                {
                    case Stretch.Baseline:
                        TrySetDriverRange(drv, handBaseline_dMin, handBaseline_dMax);
                        break;
                    case Stretch.Stretch1:
                        TrySetDriverRange(drv, handStretch1_dMin, handStretch1_dMax);
                        break;
                    case Stretch.Stretch2:
                        TrySetDriverRange(drv, handStretch2_dMin, handStretch2_dMax);
                        break;
                }
            }
        }
        else // Pinza
        {
            // Riattiva XR Index (serve per calcolare la distanza dita → gripper)
            if (block.rig.indexHider) block.rig.indexHider.SetVisible(true);

            var gc = block.rig.gripperController;
            if (gc != null)
            {
                gc.SetStretch(block.stretch); // 90 / 120 / 150 gradi
            }
            else
            {
                Debug.LogWarning("GripperController non assegnato in EffectorRig.");
            }
        }
    }


    void TrySetDriverRange(PinchOpenDriver_Interaction drv, float dMin, float dMax)
    {
        // Tenta di impostare dMin/dMax se esistono come campi pubblici
        // (se i nomi nel tuo script sono diversi, rinominali qui)
        drv.dMin = dMin;
        drv.dMax = dMax;
    }

    IEnumerator WaitForPinch(EffectorRig rig)
    {
        // partenza in pinch = openAmount <= pinchMax (timeout)
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

        // Base nel frame della reference
        Vector3 up   = anchor.up;
        Vector3 back = (anchor.forward).normalized; // indietro
        Vector3 down = (-up).normalized;             // verso il basso

        // Piano dell'arco: v1 = indietro (pallina #1), v2 = componente di "down" ortogonale a v1  (quindi GIÙ)
        Vector3 v1 = back;
        Vector3 v2 = (down - Vector3.Dot(down, v1) * v1).normalized; // down-in-plane (non negare!)

        // Centro tale che la 1ª pallina sia ESATTAMENTE sull’anchor
        Vector3 center = anchor.position + up * arcVerticalOffset - arcRadius * v1;

        float step = (ballsPerTrial > 1) ? (Mathf.Deg2Rad * arcSpanDeg / (ballsPerTrial - 1)) : 0f;

        for (int i = 0; i < ballsPerTrial; i++)
        {
            // segno NEGATIVO → curva verso DESTRA (con v2 = down-in-plane)
            float theta = -step * i;

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

    IEnumerator RunCCT(EffectorRig rig)
    {
        if (rig == null || rig.cctTask == null) yield break;

        // durante CCT della mano: mostra entrambe
        if (rig.rigType == EffectorRig.RigType.Hand)
            rig.ShowBothHandVariants(true);

        if (darkScreen) darkScreen.SetActive(true);
        yield return new WaitForSeconds(darkScreenTime);
        if (darkScreen) darkScreen.SetActive(false);

        yield return StartCoroutine(rig.cctTask.StartMisura());
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
