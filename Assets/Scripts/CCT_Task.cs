using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using System;

public class CCT_Task : MonoBehaviour
{
    [Header("Stim Targets (scene refs)")]
    public GameObject pollice;   // assegnalo dall'Inspector (opzionale)
    public GameObject indice;    // assegnalo dall'Inspector (opzionale)

    [Header("Managers on targets")]
    public misura_manager misura_pollice;   // preso da pollice.GetComponent<misura_manager>()
    public misura_manager misura_indice;    // preso da indice.GetComponent<misura_manager>()

    [Header("Trial setup")]
    public int numeroTrials = 40;           // totale trials della sessione CCT
    public int seedSync   = 40;             // quante prove sync disponibili (copiate in runtime)
    public int seedAsync  = 40;             // quante prove async disponibili (copiate in runtime)
    public float itiSeconds = 3.0f;         // finestra totale (≈ 2.9 + 0.1)

    [Header("Inputs (test)")]
    public KeyCode startKey = KeyCode.Y;    // avvio manuale (debug)
    public KeyCode respA = KeyCode.Mouse0;  // risposta "medio"
    public KeyCode respB = KeyCode.Mouse1;  // risposta "indice"

    [Header("Delays")]
    public float postResponseDelay = 1.0f;  // pausa DOPO la risposta prima del trial successivo
    public float preStartDelay     = 1.0f;  // pausa DOPO la darkscreen, PRIMA di iniziare i trials

    [Header("Logging")]
    [Tooltip("Se vuoto usa gameObject.name")]
    public string customRigId = "";         // ID del rig per file e colonna
    private string rigId;                   // risolto a Start
    private string runStamp;                // YYYYMMDD_HHMMSS per file univoco
    private string filePath;                // path completo del CSV
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
    private bool attivo;  // equivale al vecchio "interruttore == false"
    private int remainingSync;
    private int remainingAsync;

    void Start()
    {
        zeit = new Stopwatch();

        var pi = ParticipantInfo.Instance;
        nome = pi != null ? Safe(pi.Codice_Soggetto) : "NA";
        cognome = pi != null ? Safe(pi.Condizione)   : "NA";
        numeroSoggetto = pi != null ? Safe(pi.NumeroSoggetto) : "NA";

        rigId = string.IsNullOrWhiteSpace(customRigId) ? Safe(gameObject.name) : Safe(customRigId);
        runStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // Prepara directory e percorso file *unico per rig e per run*
        string directoryPath = Path.Combine(Application.dataPath, "Reports");
        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        filePath = Path.Combine(directoryPath, $"cct_{nome}_{cognome}_{rigId}_{runStamp}.csv");

        // Header
        logData = new List<string>();
        logData.Add("CodiceSoggetto,NumeroSoggetto,Condizione,RigId,Trial,Type,Response,Time(ms)");

        // Scrivi subito l'header (così il file esiste da subito)
        File.WriteAllLines(filePath, logData);
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

        // Pausa dopo la darkscreen prima di iniziare i trials
        if (preStartDelay > 0f)
            yield return new WaitForSeconds(preStartDelay);

        // Reset contatori prova
        remainingSync  = seedSync;
        remainingAsync = seedAsync;

        IsRunning = true;
        misurando = true;

        for (int i = 0; i < numeroTrials; i++)
        {
            current_Trial = i;

            // Random: 0 = 'medio' come canale luce, 1 = 'indice'
            int latoLuce  = UnityEngine.Random.Range(0, 2);
            int syncAsync = UnityEngine.Random.Range(0, 2); // 0 preferisci async, 1 preferisci sync (con fallback)

            Trial_type = "NA";
            attivo = true;
            risposta = "NA";
            zeit.Reset();

            // Pianificazione stimolo
            if (latoLuce == 0) // serie 'medio'
            {
                if ((syncAsync == 0 && remainingAsync > 0) || remainingSync == 0)
                {
                    Trial_type = "Async_medio";
                    remainingAsync--;
                    StartCoroutine(misura_pollice.accendispegni_luce());
                    StartCoroutine(misura_indice.accendispegni_haptic());
                }
                else
                {
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
                    Trial_type = "Async_indice";
                    remainingAsync--;
                    StartCoroutine(misura_indice.accendispegni_luce());
                    StartCoroutine(misura_pollice.accendispegni_haptic());
                }
                else
                {
                    Trial_type = "Sync_indice";
                    remainingSync--;
                    StartCoroutine(misura_indice.accendispegni_luce());
                    StartCoroutine(misura_indice.accendispegni_haptic());
                }
            }

            // Start timer prova
            zeit.Start();

            // Finestra di risposta (≈ itiSeconds)
            float t = 0f;
            while (t < itiSeconds && attivo)
            {
                t += Time.deltaTime;
                yield return null;
            }

            if (attivo)
            {
                // Nessuna risposta: NA
                var ms = "NA";
                zeit.Stop();
                zeit.Reset();
                risposta = "NA";
                AppendRow(ms);
                attivo = false;

                // gap breve come prima (0.1)
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

    // -- Logging helpers --

    void AppendRow(string ms)
    {
        // Riga con colonna RigId
        string row = $"{nome},{numeroSoggetto},{cognome},{rigId},{current_Trial + 1},{Trial_type},{risposta},{ms}";
        // Append sul file unico di questa run/rig
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
