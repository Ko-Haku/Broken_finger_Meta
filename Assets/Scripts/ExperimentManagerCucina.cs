using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class ExperimentManagerCucina : MonoBehaviour, IExperimentManager
{
    [Header("Configurazione Esperimento")]
    public int numeroBlocchi = 1;
    public int trialPerBlocco = 3;

    private string idSoggetto;
    private string condizioneAttuale;
    private bool esperimentoInCorso = false;
    private bool esperimentoTerminato = false;

    public Transform[] puntiSpawnFrutti;
    public Transform[] puntiSpawnBicchieri;
    public Transform[] puntiSpawnPiatti;

    public GameObject[] prefabFrutti;
    public GameObject[] prefabBicchieri;
    public GameObject[] prefabPiatti;

    public GameObject sferaPausa;
    private PausaSfera pausaSfera;

    public bool pronto = false;

    private int bloccoCorrente = 0;
    private int trialCorrente = 0;
    private int punteggioTotale = 0;
    private StreamWriter sWriter;

    public int oggettiGestitiQuestoTrial = 0;
    public const int oggettiPerTrial = 9;

    void Start()
    {
        idSoggetto = CondizioneStato.idSoggetto;
        condizioneAttuale = CondizioneStato.condizioneAttuale;

        string path = Application.dataPath + $"/logfile_cucina_{idSoggetto}_{condizioneAttuale}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
        sWriter = new StreamWriter(path, true);
        sWriter.WriteLine("ID_Soggetto,Condizione,Blocco,Trial,Categoria,NomeOggetto,NomeContenitore,Corretto,PuntiTrial,PunteggioTotale");

        pausaSfera = sferaPausa.GetComponent<PausaSfera>();

        if (!esperimentoInCorso)
        {
            esperimentoInCorso = true;
            StartCoroutine(ControllaAvvioEsperimento());
        }
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

            yield return StartCoroutine(EseguiBlocco());
        }

        Debug.Log("⌛ In attesa degli ultimi rilasci per terminare l'esperimento.");
    }

    IEnumerator EseguiBlocco()
    {
        trialCorrente = 0;
        while (trialCorrente < trialPerBlocco)
        {
            trialCorrente++;
            oggettiGestitiQuestoTrial = 0;

            Debug.Log($"▶️ Blocco {bloccoCorrente} - Trial {trialCorrente}");

            foreach (Transform t in puntiSpawnFrutti)
            {
                int i = Random.Range(0, prefabFrutti.Length);
                Instantiate(prefabFrutti[i], t.position, Quaternion.identity);
            }

            foreach (Transform t in puntiSpawnBicchieri)
            {
                int i = Random.Range(0, prefabBicchieri.Length);
                Instantiate(prefabBicchieri[i], t.position, Quaternion.identity);
            }

            foreach (Transform t in puntiSpawnPiatti)
            {
                int i = Random.Range(0, prefabPiatti.Length);
                Instantiate(prefabPiatti[i], t.position, Quaternion.Euler(90f, 0f, 0f));
            }

            yield return new WaitForSeconds(6f); // durata trial
        }

        Debug.Log($"✅ Blocco {bloccoCorrente} completato.");
    }

    public void LogOggetto(string categoria, string oggetto, string contenitore, bool corretto)
    {
        int puntiTrial = corretto ? 2 : -1;
        punteggioTotale += puntiTrial;

        if (sWriter != null)
        {
            sWriter.WriteLine($"{idSoggetto},{condizioneAttuale},{bloccoCorrente},{trialCorrente},{categoria},{oggetto},{contenitore},{(corretto ? "SI" : "NO")},{puntiTrial},{punteggioTotale}");
            sWriter.Flush();
        }

        Debug.Log($"LOG: {categoria} {oggetto} → {contenitore} | {(corretto ? "✅" : "❌")} | Punti: {puntiTrial} | Totale: {punteggioTotale}");

        oggettiGestitiQuestoTrial++;

        if (bloccoCorrente == numeroBlocchi && trialCorrente == trialPerBlocco && oggettiGestitiQuestoTrial >= oggettiPerTrial)
        {
            FineEsperimento();
        }
    }

    private void FineEsperimento()
    {
        if (!esperimentoTerminato)
        {
            esperimentoTerminato = true;
            sWriter.Close();
            sWriter = null;
            Debug.Log("✅ Esperimento completato dopo rilascio di tutti gli oggetti.");
        }
    }

    public void SegnalaPronto()
    {
        if (esperimentoTerminato) return;
        pronto = true;
    }
}
