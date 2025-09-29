using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class ExperimentManagerCubetti : MonoBehaviour, IExperimentManager
{
    private int cubettiGestitiQuestoTrial = 0;
    private const int cubettiPerTrial = 9;

    [Header("Configurazione Esperimento")]
    public int numeroBlocchi = 1;
    public int trialPerBlocco = 9;

    private string idSoggetto;
    private string condizioneAttuale;
    private bool esperimentoInCorso = false;
    private bool esperimentoTerminato = false;

    public Transform[] puntiSpawn; // deve avere almeno 9 punti
    public GameObject[] prefabCubetti; // 0 = Rosso, 1 = Verde, 2 = Blu
    public GameObject sferaPausa;

    public bool pronto = false;

    private int bloccoCorrente = 0;
    private int trialCorrente = 0;
    private int punteggioTotale = 0;

    private StreamWriter sWriter;
    private PausaSfera pausaSfera;

    void Start()
    {
        idSoggetto = CondizioneStato.idSoggetto;
        condizioneAttuale = CondizioneStato.condizioneAttuale;

        string path = Application.dataPath + $"/logfile_cubetti_{idSoggetto}_{condizioneAttuale}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
        sWriter = new StreamWriter(path, true);
        sWriter.WriteLine("ID_Soggetto,Condizione,Blocco,Trial,ColoreCubetto,ColoreContenitore,Corretto,PuntiTrial,PunteggioTotale");

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

        // Non chiudiamo qui! Il CSV verrà chiuso da FineEsperimento() dopo l’ultimo cubetto
    }

    IEnumerator EseguiBlocco()
    {
        trialCorrente = 0;

        while (trialCorrente < trialPerBlocco)
        {
            trialCorrente++;
            cubettiGestitiQuestoTrial = 0;
            Debug.Log($"▶️ Blocco {bloccoCorrente} - Trial {trialCorrente}");

            List<int> indici = new List<int>();
            for (int i = 0; i < puntiSpawn.Length; i++) indici.Add(i);
            Shuffle(indici);

            List<GameObject> cubetti = new List<GameObject>();
            int spawnIndex = 0;

            for (int colore = 0; colore < 3; colore++) // 3 colori
            {
                for (int i = 0; i < 3; i++) // 3 cubetti per colore
                {
                    if (spawnIndex >= indici.Count) break;

                    GameObject cubo = Instantiate(
                        prefabCubetti[colore],
                        puntiSpawn[indici[spawnIndex]].position,
                        Quaternion.identity
                    );
                    cubetti.Add(cubo);
                    spawnIndex++;
                }
            }

            // Attendi che il soggetto interagisca con tutti i 9 cubetti
            while (cubettiGestitiQuestoTrial < cubettiPerTrial)
                yield return null;
        }

        Debug.Log($"✅ Blocco {bloccoCorrente} completato.");

        // Se è l'ultimo blocco, chiudiamo l’esperimento
        if (bloccoCorrente == numeroBlocchi)
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
            Debug.Log("✅ Esperimento completato (dopo rilascio di tutti i cubetti).");
        }
    }

    public void LogCubetto(string coloreCubetto, string coloreContenitore, bool corretto)
    {
        if (sWriter == null) return;

        int puntiTrial = corretto ? 2 : -1;
        punteggioTotale += puntiTrial;

        sWriter.WriteLine($"{idSoggetto},{condizioneAttuale},{bloccoCorrente},{trialCorrente},{coloreCubetto},{coloreContenitore},{(corretto ? "SI" : "NO")},{puntiTrial},{punteggioTotale}");
        sWriter.Flush();

        Debug.Log($"LOG: {coloreCubetto} → {coloreContenitore} | {(corretto ? "✅" : "❌")} | Punti: {puntiTrial} | Totale: {punteggioTotale}");

        cubettiGestitiQuestoTrial++;
    }

    public void SegnalaPronto()
    {
        if (esperimentoTerminato) return;
        pronto = true;
    }

    void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
