using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public class CCT_Task : MonoBehaviour
{
    [Header("Stim Targets (scene refs)")]
    public GameObject pollice;   // assegnare dall'Inspector (opzionale)
    public GameObject indice;    // assegnare dall'Inspector (opzionale)

    [Header("Managers on targets")]
    public misura_manager misura_pollice;   // preso da pollice.GetComponent<misura_manager>()
    public misura_manager misura_indice;    // preso da indice.GetComponent<misura_manager>()

    [Header("Trial setup")]
    [Tooltip("Quanti trial per ciascuna categoria (Sync/Async x Pollice/Indice). 5 => totale 20.")]
    public int countPerType = 5;
    [Tooltip("Durata finestra di risposta per ciascun trial (secondi).")]
    public float trialWindowSec = 3.0f;

    [Header("Delays")]
    [Tooltip("Pausa DOPO la darkscreen e PRIMA di iniziare i trials.")]
    public float preStartDelay = 1.0f;
    [Tooltip("Pausa DOPO una risposta prima di passare al trial successivo.")]
    public float postResponseDelay = 1.0f;

    [Header("Input (test)")]
    public KeyCode startKey = KeyCode.Y;          // avvio manuale (debug)
    public KeyCode respPollice = KeyCode.Mouse1;  // risposta "pollice"
    public KeyCode respIndice  = KeyCode.Mouse0;  // risposta "indice"

    [Header("Bilanciamento schedule")]
    [Tooltip("Massimo numero di trial consecutivi con la stessa categoria (es. 2).")]
    public int maxSameInRow = 2;
    [Tooltip("Seed per randomizzazione (-1 = random di Unity).")]
    public int randomSeed = -1;

    [Header("Logging / File")]
    [Tooltip("Se vuoto usa gameObject.name")]
    public string customRigId = "";    // ID del rig per file e colonna
    private string rigId;              // risolto a Start
    private string runStamp;           // YYYYMMDD_HHMMSS per file univoco
    private string filePath;           // path completo del CSV
    private List<string> logData;

    [Header("Condition override (set by ExperimentRT)")]
    [Tooltip("Impostato da ExperimentRT (es. Hand_Baseline, Hand_Stretch1, Pinza_Stretch2)")]
    public string overrideCondition = null;

    // runtime
    public bool IsRunning { get; private set; }
    public static bool misurando = false;

    private Stopwatch zeit;
    private int current_Trial;
    private string risposta;
    private string Trial_type;
    private string nome;
    private string cognome;
    private string numeroSoggetto;

    // schedule 4 categorie
    private enum Cat { SyncPollice, SyncIndice, AsyncPollice, AsyncIndice }
    private List<Cat> schedule;  // lunga 4 * countPerType
    private bool attivo;         // finestra risposta attiva

    private string currentCondition; // condizione corrente stampata nel CSV

    // ---- API chiamata da ExperimentRT per impostare la condizione corrente ----
    public void SetCondition(string cond)
    {
        overrideCondition = cond;
        currentCondition  = cond; // usata nel logging
    }

    void Start()
    {
        if (randomSeed >= 0) UnityEngine.Random.InitState(randomSeed);

        zeit = new Stopwatch();

        var pi = ParticipantInfo.Instance;
        nome           = pi != null ? Safe(pi.Codice_Soggetto)   : "NA";
        cognome        = pi != null ? Safe(pi.Condizione)         : "NA";
        numeroSoggetto = pi != null ? Safe(pi.NumeroSoggetto)     : "NA";

        // Se ExperimentRT non ha ancora impostato la condizione, fallback
        currentCondition = string.IsNullOrEmpty(overrideCondition) ? cognome : overrideCondition;

        rigId    = string.IsNullOrWhiteSpace(customRigId) ? Safe(gameObject.name) : Safe(customRigId);
        runStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // Prepara directory e percorso file *unico per rig e per run*
        string directoryPath = Path.Combine(Application.dataPath, "Reports");
        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        filePath = Path.Combine(directoryPath, $"cct_{nome}_{cognome}_{rigId}_{runStamp}.csv");

        // Header
        logData = new List<string>();
        logData.Add("CodiceSoggetto,NumeroSoggetto,Condizione,RigId,Trial,Type,Response,Time(ms)");
        File.WriteAllLines(filePath, logData); // crea subito il file con header
    }

    void Update()
    {
        // Avvio manuale per debug
        if (Input.GetKeyDown(startKey) && !IsRunning)
            StartCoroutine(StartMisura());

        if (!IsRunning) return;

        // Gestione risposta durante la prova
        if (attivo)
        {
            if (Input.GetKeyDown(respPollice))
                MisuraTerminata("pollice");

            if (Input.GetKeyDown(respIndice))
                MisuraTerminata("indice");
        }
    }

    public System.Collections.IEnumerator StartMisura()
    {
        // Setup riferimenti (fallback via Find se non assegnati)
        if (pollice == null) pollice = GameObject.Find("pollice");
        if (indice  == null) indice  = GameObject.Find("indice");

        if (pollice == null || indice == null)
        {
            UnityEngine.Debug.LogError($"[{rigId}] CCT_Task: 'pollice' o 'indice' non trovati in scena.");
            yield break;
        }

        misura_pollice = pollice.GetComponent<misura_manager>();
        misura_indice  = indice.GetComponent<misura_manager>();

        if (misura_pollice == null || misura_indice == null)
        {
            UnityEngine.Debug.LogError($"[{rigId}] CCT_Task: manca 'misura_manager' su pollice/indice.");
            yield break;
        }

        // Pausa dopo la darkscreen (gestita fuori) prima di iniziare i trials
        if (preStartDelay > 0f)
            yield return new WaitForSeconds(preStartDelay);

        // Costruisci schedule bilanciata ed esatta: 4 categorie * countPerType
        BuildExactBalancedSchedule(countPerType);

        IsRunning = true;
        misurando = true;

        int numeroTrials = schedule.Count;

        for (int i = 0; i < numeroTrials; i++)
        {
            current_Trial = i;

            // Categoria per questo trial
            Cat cat = schedule[i];
            Trial_type = cat.ToString();
            attivo = true;
            risposta = "NA";
            zeit.Reset();

            // Pianificazione stimolo (luce + haptic)
            switch (cat)
            {
                case Cat.SyncPollice:
                    StartCoroutine(misura_pollice.accendispegni_luce());
                    StartCoroutine(misura_pollice.accendispegni_haptic());
                    break;

                case Cat.AsyncPollice:
                    StartCoroutine(misura_pollice.accendispegni_luce());
                    StartCoroutine(misura_indice.accendispegni_haptic());
                    break;

                case Cat.SyncIndice:
                    StartCoroutine(misura_indice.accendispegni_luce());
                    StartCoroutine(misura_indice.accendispegni_haptic());
                    break;

                case Cat.AsyncIndice:
                    StartCoroutine(misura_indice.accendispegni_luce());
                    StartCoroutine(misura_pollice.accendispegni_haptic());
                    break;
            }

            // Start timer prova
            zeit.Start();

            // Finestra di risposta
            float t = 0f;
            while (t < trialWindowSec && attivo)
            {
                t += Time.deltaTime;
                yield return null;
            }

            if (attivo)
            {
                // Nessuna risposta: NA
                string ms = "NA";
                zeit.Stop();
                zeit.Reset();
                AppendRow(ms);
                attivo = false;

                // piccolo gap “storico”
                yield return new WaitForSeconds(0.1f);
            }
            else
            {
                // risposta arrivata → delay prima del prossimo trial
                if (postResponseDelay > 0f)
                    yield return new WaitForSeconds(postResponseDelay);
            }
        }

        misurando = false;
        IsRunning = false;
        yield break;
    }

    // ===== Schedule bilanciata ed ESATTA (5/5/5/5 per default) =====

    private void BuildExactBalancedSchedule(int perType)
    {
        if (perType <= 0) perType = 1; // safety

        var bag = new List<Cat>(perType * 4);
        bag.AddRange(Repeat(Cat.SyncPollice,  perType));
        bag.AddRange(Repeat(Cat.SyncIndice,   perType));
        bag.AddRange(Repeat(Cat.AsyncPollice, perType));
        bag.AddRange(Repeat(Cat.AsyncIndice,  perType));

        // Mescola e ripara per rispettare maxSameInRow
        const int maxAttempts = 400;
        List<Cat> best = null;
        int bestScore = int.MaxValue;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            FisherYates(bag);
            var candidate = new List<Cat>(bag);
            int penalty = ConsecutivePenalty(candidate, maxSameInRow);

            // piccola ricerca locale
            int localTries = 500;
            while (penalty > 0 && localTries-- > 0)
            {
                int i = UnityEngine.Random.Range(0, candidate.Count);
                int j = UnityEngine.Random.Range(0, candidate.Count);
                (candidate[i], candidate[j]) = (candidate[j], candidate[i]);
                int newPenalty = ConsecutivePenalty(candidate, maxSameInRow);
                if (newPenalty <= penalty) penalty = newPenalty;
                else (candidate[i], candidate[j]) = (candidate[j], candidate[i]);
            }

            if (penalty < bestScore)
            {
                bestScore = penalty;
                best = new List<Cat>(candidate);
                if (bestScore == 0) break;
            }
        }

        schedule = best ?? bag;

        // Safety net: se ancora violazioni, faccio una riparazione lineare
        RepairRuns(schedule, maxSameInRow);
    }

    private static IEnumerable<T> Repeat<T>(T v, int n)
    {
        for (int i = 0; i < n; i++) yield return v;
    }

    private static void FisherYates<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // Conta violazioni della run-length massima per categoria
    private static int ConsecutivePenalty(List<Cat> seq, int maxRun)
    {
        int pen = 0;
        int run = 1;
        for (int i = 1; i < seq.Count; i++)
        {
            if (seq[i] == seq[i - 1]) { run++; if (run > maxRun) pen++; }
            else run = 1;
        }
        return pen;
    }

    // Ripara linearmente le run troppo lunghe scambiando con l’elemento più vicino compatibile
    private static void RepairRuns(List<Cat> seq, int maxRun)
    {
        if (seq.Count == 0) return;
        int run = 1;
        for (int i = 1; i < seq.Count; i++)
        {
            if (seq[i] == seq[i - 1])
            {
                run++;
                if (run > maxRun)
                {
                    // cerca un j da scambiare che non crei una run eccessiva
                    for (int j = i + 1; j < seq.Count; j++)
                    {
                        if (seq[j] != seq[i])
                        {
                            // verifica vicinanze dopo swap
                            var a = seq[i - 1];
                            var b = seq[j];
                            var before_i = (i - 1 >= 0) ? seq[i - 1] : (Cat)(-1);
                            var after_i  = (i + 1 < seq.Count) ? seq[i + 1] : (Cat)(-1);
                            var before_j = (j - 1 >= 0) ? seq[j - 1] : (Cat)(-1);
                            var after_j  = (j + 1 < seq.Count) ? seq[j + 1] : (Cat)(-1);

                            bool ok_i = (b != before_i) && (b != after_i);
                            bool ok_j = (seq[i] != before_j) && (seq[i] != after_j);

                            if (ok_i && ok_j)
                            {
                                (seq[i], seq[j]) = (seq[j], seq[i]);
                                run = 1; // reset run
                                break;
                            }
                        }
                    }
                }
            }
            else run = 1;
        }
    }

    // ===== Risposte e logging =====

    private void MisuraTerminata(string resp)
    {
        if (!attivo) return;

        zeit.Stop();
        string MS = zeit.ElapsedMilliseconds.ToString();
        zeit.Reset();
        risposta = resp;
        AppendRow(MS);
        attivo = false;
    }

    private void AppendRow(string ms)
    {
        // Se ExperimentRT non ha chiamato SetCondition, fallback a cognome
        string cond = string.IsNullOrEmpty(currentCondition) ? cognome : currentCondition;

        string row = $"{nome},{numeroSoggetto},{cond},{rigId},{current_Trial + 1},{Trial_type},{risposta},{ms}";
        File.AppendAllLines(filePath, new[] { row });
    }

    private static string Safe(string s)
    {
        if (string.IsNullOrEmpty(s)) return "NA";
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }
}
