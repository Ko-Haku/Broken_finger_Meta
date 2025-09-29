using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Diagnostics;

public class CCT_Task : MonoBehaviour
{
    [Header("Stim Targets (scene refs)")]
    public GameObject pollice;   // assegnalo dall'Inspector (opzionale)
    public GameObject indice;    // assegnalo dall'Inspector (opzionale)

    [Header("Managers on targets")]
    public misura_manager misura_pollice;   // viene preso da pollice.GetComponent<misura_manager>()
    public misura_manager misura_indice;    // viene preso da indice.GetComponent<misura_manager>()

    [Header("Trial setup")]
    public int numeroTrials = 40;    // totale trials della sessione CCT
    public int seedSync   = 40;      // quante prove sync disponibili (verrà copiato in runtime)
    public int seedAsync  = 40;      // quante prove async disponibili (verrà copiato in runtime)
    public float itiSeconds = 3.0f;  // tempo tra prove (2.9 + 0.1 nel tuo codice)

    [Header("Inputs (test)")]
    public KeyCode startKey = KeyCode.Y;    // avvio manuale (per debug)
    public KeyCode respA = KeyCode.Mouse0;  // risposta "medio"
    public KeyCode respB = KeyCode.Mouse1;  // risposta "indice"

    [Header("Logging")]
    public List<string> logData;

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

    // Stato risposta per questa trial
    private bool attivo;  // equivale al tuo "interruttore == false"
    private int remainingSync;
    private int remainingAsync;

    void Start()
    {
        zeit = new Stopwatch();
        var pi = ParticipantInfo.Instance;
        nome = pi != null ? pi.Codice_Soggetto : "NA";
        cognome = pi != null ? pi.Condizione : "NA";
        numeroSoggetto = pi != null ? pi.NumeroSoggetto : "NA";

        logData = new List<string>();
        logData.Add("CodiceSoggetto,NumeroSoggetto,Condizione,Trial,Type,Response,Time(ms)");
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
            if (Input.GetKeyDown(respA))
                MisuraTerminata("medio");

            if (Input.GetKeyDown(respB))
                MisuraTerminata("indice");
        }
    }

    public IEnumerator StartMisura()
    {
        // Setup riferimenti (fallback via Find se non assegnati)
        if (pollice == null) pollice = GameObject.Find("pollice");
        if (indice  == null) indice  = GameObject.Find("indice");

        if (pollice == null || indice == null)
        {
            UnityEngine.Debug.LogError("CCT_Task: 'pollice' o 'indice' non trovati in scena. " +
                                       "Assegna i GameObject o rinominali correttamente.");
            yield break;
        }

        misura_pollice = pollice.GetComponent<misura_manager>();
        misura_indice  = indice.GetComponent<misura_manager>();

        if (misura_pollice == null || misura_indice == null)
        {
            UnityEngine.Debug.LogError("CCT_Task: manca 'misura_manager' su pollice/indice.");
            yield break;
        }

        // Reset contatori prova
        remainingSync  = seedSync;
        remainingAsync = seedAsync;

        IsRunning = true;
        misurando = true;

        for (int i = 0; i < numeroTrials; i++)
        {
            current_Trial = i;

            // Random: 0 = 'medio' come canale luce, 1 = 'indice' come canale luce
            int latoLuce = Random.Range(0, 2);
            int syncAsync = Random.Range(0, 2); // 0 preferisci async, 1 preferisci sync (con fallback)

            Trial_type = "NA";
            attivo = true;
            risposta = "NA";
            zeit.Reset();

            // Pianificazione stimolo
            if (latoLuce == 0) // serie 'medio'
            {
                if ((syncAsync == 0 && remainingAsync > 0) || remainingSync == 0)
                {
                    // Async_medio: luce medio + haptic indice
                    Trial_type = "Async_medio";
                    remainingAsync--;
                    StartCoroutine(misura_pollice.accendispegni_luce());
                    StartCoroutine(misura_indice.accendispegni_haptic());
                }
                else
                {
                    // Sync_medio: luce medio + haptic medio
                    Trial_type = "Sync_medio";
                    remainingSync--;
                    StartCoroutine(misura_pollice.accendispegni_luce());
                    StartCoroutine(misura_pollice.accendispegni_haptic());
                }
            }
            else // latoLuce == 1 → serie 'indice'
            {
                if ((syncAsync == 0 && remainingAsync > 0) || remainingSync == 0)
                {
                    // Async_indice: luce indice + haptic medio
                    Trial_type = "Async_indice";
                    remainingAsync--;
                    StartCoroutine(misura_indice.accendispegni_luce());
                    StartCoroutine(misura_pollice.accendispegni_haptic());
                }
                else
                {
                    // Sync_indice: luce + haptic indice
                    Trial_type = "Sync_indice";
                    remainingSync--;
                    StartCoroutine(misura_indice.accendispegni_luce());
                    StartCoroutine(misura_indice.accendispegni_haptic());
                }
            }

            // Start timer prova
            zeit.Start();

            // Finestra di risposta (≈ 3 s)
            float t = 0f;
            while (t < itiSeconds && attivo)
            {
                t += Time.deltaTime;
                yield return null;
            }

            // Se non è arrivata risposta, segna NA
            if (attivo)
            {
                var ms = "NA";
                zeit.Stop();
                zeit.Reset();
                risposta = "NA";
                SaveData(ms);
                attivo = false;
            }

            // Piccolo gap extra (equivaleva al tuo 0.1f)
            yield return new WaitForSeconds(0.1f);
        }

        misurando = false;
        IsRunning = false;
        // niente experiment.via = true; (era per il vecchio manager)
        yield break;
    }

    void MisuraTerminata(string resp)
    {
        if (!attivo) return;

        zeit.Stop();
        var MS = zeit.ElapsedMilliseconds.ToString();
        zeit.Reset();
        risposta = resp;
        SaveData(MS);
        attivo = false;
    }

    void SaveData(string ms)
    {
        string logEntry = $"{nome},{numeroSoggetto},{cognome},{current_Trial + 1},{Trial_type},{risposta},{ms}";
        logData.Add(logEntry);
        SaveLogToFile();
    }

    public void SaveLogToFile()
    {
        string directoryPath = Path.Combine(Application.dataPath, "Reports");
        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        string filePath = Path.Combine(directoryPath, $"cct_{nome}_{cognome}.csv");
        File.WriteAllLines(filePath, logData);
        UnityEngine.Debug.Log($"CCT saved: {filePath}");
    }
}
