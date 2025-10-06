using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public class CCT_Task : MonoBehaviour
{
    [Header("Stim Targets (scene refs)")]
    public GameObject pollice;
    public GameObject indice;

    [Header("Managers on targets")]
    public misura_manager misura_pollice;
    public misura_manager misura_indice;

    [Header("Bilanciamento per categoria (per type)")]
    [Tooltip("Quante prove per ciascuna categoria: Sync/Async x Pollice/Indice (tot=4*countPerType)")]
    public int countPerType = 10;   // 10→totale 40

    [Header("Durate")]
    public float itiSeconds = 3.0f;     // finestra di risposta
    public float postResponseDelay = 1.0f;
    public float preStartDelay = 1.0f;

    [Header("Inputs (test)")]
    public KeyCode startKey = KeyCode.Y;
    public KeyCode respPollice = KeyCode.Mouse1;
    public KeyCode respIndice  = KeyCode.Mouse0;

    [Header("Logging")]
    public string customRigId = "";
    private string rigId, runStamp, filePath;
    public List<string> logData;

    [Header("Condition override (set by ExperimentRT)")]
    public string overrideCondition = null;
    public void SetCondition(string cond) => overrideCondition = cond;

    // runtime
    public bool IsRunning { get; private set; }
    public static bool misurando = false;

    private Stopwatch zeit;
    private int current_Trial;
    private string risposta, Trial_type;
    private string nome, cognome, numeroSoggetto;
    private bool attivo;

    // coda bilanciata
    private enum Cat { SyncPollice, AsyncPollice, SyncIndice, AsyncIndice }
    private List<Cat> schedule = new List<Cat>();

    void Start()
    {
        zeit = new Stopwatch();

        var pi = ParticipantInfo.Instance;
        nome = pi != null ? Safe(pi.Codice_Soggetto)   : "NA";
        cognome = pi != null ? Safe(pi.Condizione)     : "NA";
        numeroSoggetto = pi != null ? Safe(pi.NumeroSoggetto) : "NA";

        rigId = string.IsNullOrWhiteSpace(customRigId) ? Safe(gameObject.name) : Safe(customRigId);
        runStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        string directoryPath = Path.Combine(Application.dataPath, "Reports");
        if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
        filePath = Path.Combine(directoryPath, $"cct_{nome}_{cognome}_{rigId}_{runStamp}.csv");

        logData = new List<string>();
        logData.Add("CodiceSoggetto,NumeroSoggetto,Condizione,RigId,Trial,Type,Response,Time(ms)");
        File.WriteAllLines(filePath, logData);

        // prepara schedule bilanciata
        BuildBalancedSchedule();
    }

    void Update()
    {
        if (Input.GetKeyDown(startKey) && !IsRunning)
            StartCoroutine(StartMisura());

        if (!IsRunning) return;

        if (attivo)
        {
            if (Input.GetKeyDown(respPollice)) MisuraTerminata("pollice");
            if (Input.GetKeyDown(respIndice))  MisuraTerminata("indice");
        }
    }

    void BuildBalancedSchedule()
    {
        schedule.Clear();
        for (int i = 0; i < countPerType; i++)
        {
            schedule.Add(Cat.SyncPollice);
            schedule.Add(Cat.AsyncPollice);
            schedule.Add(Cat.SyncIndice);
            schedule.Add(Cat.AsyncIndice);
        }
        // Fisher–Yates
        var rng = new System.Random();
        for (int i = schedule.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (schedule[i], schedule[j]) = (schedule[j], schedule[i]);
        }
    }

    public IEnumerator StartMisura()
    {
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

        if (preStartDelay > 0f) yield return new WaitForSeconds(preStartDelay);

        IsRunning = true;
        misurando = true;

        int numeroTrials = schedule.Count;

        for (int i = 0; i < numeroTrials; i++)
        {
            current_Trial = i;
            var cat = schedule[i];

            Trial_type = "NA";
            attivo = true;
            risposta = "NA";
            zeit.Reset();

            // pianifica in base alla categoria
            switch (cat)
            {
                case Cat.SyncPollice:
                    Trial_type = "Sync_pollice";
                    StartCoroutine(misura_pollice.accendispegni_luce());
                    StartCoroutine(misura_pollice.accendispegni_haptic());
                    break;

                case Cat.AsyncPollice:
                    Trial_type = "Async_pollice";
                    StartCoroutine(misura_pollice.accendispegni_luce());
                    StartCoroutine(misura_indice.accendispegni_haptic());
                    break;

                case Cat.SyncIndice:
                    Trial_type = "Sync_indice";
                    StartCoroutine(misura_indice.accendispegni_luce());
                    StartCoroutine(misura_indice.accendispegni_haptic());
                    break;

                case Cat.AsyncIndice:
                    Trial_type = "Async_indice";
                    StartCoroutine(misura_indice.accendispegni_luce());
                    StartCoroutine(misura_pollice.accendispegni_haptic());
                    break;
            }

            zeit.Start();

            float t = 0f;
            while (t < itiSeconds && attivo)
            {
                t += Time.deltaTime;
                yield return null;
            }

            if (attivo)
            {
                var ms = "NA";
                zeit.Stop(); zeit.Reset();
                risposta = "NA";
                AppendRow(ms);
                attivo = false;
                yield return new WaitForSeconds(0.1f);
            }
            else
            {
                if (postResponseDelay > 0f)
                    yield return new WaitForSeconds(postResponseDelay);
            }
        }

        misurando = false;
        IsRunning = false;
        yield break;
    }

    void MisuraTerminata(string resp)
    {
        if (!attivo) return;
        zeit.Stop();
        var MS = zeit.ElapsedMilliseconds.ToString();
        zeit.Reset();
        risposta = resp;
        AppendRow(MS);
        attivo = false;
    }

    void AppendRow(string ms)
    {
        // NB: overrideCondition può essere null → mettiamo "NA"
        string cond = string.IsNullOrEmpty(overrideCondition) ? "NA" : overrideCondition;
        string row = $"{nome},{numeroSoggetto},{cond},{rigId},{current_Trial + 1},{Trial_type},{risposta},{ms}";
        File.AppendAllLines(filePath, new[] { row });
    }

    static string Safe(string s)
    {
        if (string.IsNullOrEmpty(s)) return "NA";
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }
}
