using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class ExperimentManager : MonoBehaviour, IExperimentManager
{
    [Header("Configurazione Esperimento")]
    public int numeroBlocchi = 1;
    public int trialPerBlocco = 9;

    public Transform[] puntiSpawn;
    public GameObject[] prefabColori; // 0 = rosso, 1 = blu, 2 = giallo
    public GameObject[] riferimentiColori;
    public Transform puntoSferaRiferimento;
    public GameObject sferaPausa;

    public bool pronto = false;
    public bool fasePratica = false;

    private string idSoggetto;
    private string toolUsato;
    private int bloccoCorrente = 0;
    private int trialCorrente = 0;
    private int punteggioTotale = 0;
    private int coloreTarget;
    private StreamWriter sWriter;
    private Stopwatch cronometro = new Stopwatch();
    private PausaSfera pausaSfera;
    private bool esperimentoTerminato = false;

    void Start()
    {
        idSoggetto = CondizioneStato.idSoggetto;
        toolUsato = CondizioneStato.condizioneAttuale;

        string path = Path.Combine(Application.dataPath, $"logfile_{idSoggetto}_{toolUsato}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        sWriter = new StreamWriter(path, true);
        sWriter.WriteLine("ID_Soggetto,Blocco,Trial,Tool,ColoreTarget,ColoreSfera,TempoSpawn,TempoFineTrial,Durata,TipoTrial,PuntiTrial,PunteggioTotale");

        pausaSfera = sferaPausa.GetComponent<PausaSfera>();

        StartCoroutine(ControllaAvvioEsperimento());
    }

    IEnumerator ControllaAvvioEsperimento()
    {
        for (int i = 0; i < numeroBlocchi; i++)
        {
            bloccoCorrente = i + 1;

            pausaSfera.AttivaSfera();
            Debug.Log($"⏸ In attesa per blocco {bloccoCorrente}... Tocca la sfera per iniziare.");

            while (!pronto)
                yield return null;

            pronto = false;
            pausaSfera.DisattivaSfera();

            yield return StartCoroutine(EseguiBlocco(bloccoCorrente));
        }

        sWriter.Close();
        esperimentoTerminato = true;
        Debug.Log("✅ Esperimento completato.");
    }

    IEnumerator EseguiBlocco(int blocco)
    {
        trialCorrente = 0;

        while (trialCorrente < trialPerBlocco)
        {
            trialCorrente++;
            Debug.Log($"▶️ Blocco {bloccoCorrente} - Trial {trialCorrente}");

            coloreTarget = Random.Range(0, 3);
            GameObject sferaRif = Instantiate(riferimentiColori[coloreTarget], puntoSferaRiferimento.position, Quaternion.identity);
            yield return new WaitForSeconds(2f);
            Destroy(sferaRif);

            List<int> indici = new List<int>();
            for (int i = 0; i < puntiSpawn.Length; i++) indici.Add(i);
            Shuffle(indici);

            List<GameObject> sfereIstanziate = new List<GameObject>();
            cronometro.Reset();
            cronometro.Start();
            string tempoSpawn = System.DateTime.Now.ToString("HH:mm:ss.fff");

            for (int colore = 0; colore < 3; colore++)
            {
                for (int i = 0; i < 3; i++)
                {
                    int idx = indici[colore * 3 + i];
                    GameObject sfera = Instantiate(prefabColori[colore], puntiSpawn[idx].position, Quaternion.identity);
                    SferaTarget target = sfera.GetComponent<SferaTarget>();
                    if (target != null)
                    {
                        target.tempoSpawn = tempoSpawn;
                        target.cronometro = cronometro;
                    }
                    sfereIstanziate.Add(sfera);

                }
            }

            yield return new WaitForSeconds(4f);
            cronometro.Stop();
            string tempoFine = System.DateTime.Now.ToString("HH:mm:ss.fff");
            string durata = cronometro.Elapsed.TotalSeconds.ToString("F3");

            foreach (GameObject s in sfereIstanziate)
            {
                if (s != null)
                {
                    Destroy(s); // distrugge solo le sfere non esplose
                }
            }

            yield return new WaitForSeconds(1f);
        }

        Debug.Log($"✅ Blocco {bloccoCorrente} completato.");
    }

    public void SegnaPunto(string coloreSfera)
    {
        if (esperimentoTerminato) return;

        string timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");
        string tipo = fasePratica ? "PRATICA" : "SPERIMENTALE";

        bool corretto = coloreTarget == ColoreToIndex(coloreSfera);
        string correttezza = corretto ? "CORRETTO" : "ERRORE";

        int punteggioTrial = corretto ? 2 : -1;
        punteggioTotale += punteggioTrial;

        sWriter.WriteLine($"{idSoggetto},{bloccoCorrente},{trialCorrente},{toolUsato},{coloreTarget},{coloreSfera},{timestamp},RISPOSTA,,{tipo},{punteggioTrial},{punteggioTotale}");
        sWriter.Flush();

        Debug.Log($"📝 Risposta: {correttezza} | Punti: {punteggioTrial} | Totale: {punteggioTotale}");
    }

    public void LogSfera(string colore, string tempoSpawn, string tempoFine, string durata)
    {
        string tipo = fasePratica ? "PRATICA" : "SPERIMENTALE";

        sWriter.WriteLine($"{idSoggetto},{bloccoCorrente},{trialCorrente},{toolUsato},{coloreTarget},{colore},{tempoSpawn},{tempoFine},{durata},{tipo},0,{punteggioTotale}");
        sWriter.Flush();
    }

    int ColoreToIndex(string colore)
    {
        switch (colore.ToLower())
        {
            case "rosso": return 0;
            case "blu": return 1;
            case "giallo": return 2;
            default: return -1;
        }
    }

    void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public void SegnalaPronto()
    {
        if (esperimentoTerminato) return;
        pronto = true;
    }
}
