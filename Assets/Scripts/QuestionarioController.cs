using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;
using System.IO;

public class QuestionarioController : MonoBehaviour
{
    [System.Serializable]
    public class Question
    {
        public string id;
        public string testoConPlaceholder;

        public string GetTesto(string condizione)
        {
            return testoConPlaceholder.Replace("____", condizione);
        }
    }

    [System.Serializable]
    public class QuestionListWrapper
    {
        public List<Question> domande;
    }

    [Header("Domande e UI")]
    public List<Question> domande;
    public TextMeshProUGUI testoDomanda;

    private int indiceDomanda = 0;
    private bool rispostaInAttesa = true;

    private string idSoggetto;
    private string condizioneAttuale;
    private string scenaProvenienza;
    private string logFilePath;

    void Start()
    {
        // ✅ Prendi tutto da CondizioneStato
        idSoggetto = CondizioneStato.idSoggetto;
        condizioneAttuale = CondizioneStato.condizioneAttuale;
        scenaProvenienza = CondizioneStato.scenaPrecedente;

        CaricaDomandeDaJson();
        RandomizzaDomande();
        MostraProssimaDomanda();

        // ✅ Log file includendo anche la scena di provenienza
        logFilePath = Path.Combine(Application.dataPath, $"questionario_log_{idSoggetto}_{condizioneAttuale}_{scenaProvenienza}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
        using (StreamWriter writer = new StreamWriter(logFilePath))
        {
            writer.WriteLine("ID_Soggetto,Condizione,ScenaProvenienza,DomandaID,TestoDomanda,RispostaScelta,Timestamp");
        }
    }

    void CaricaDomandeDaJson()
    {
        TextAsset jsonText = Resources.Load<TextAsset>("questionario_embodiment");
        if (jsonText != null)
        {
            QuestionListWrapper wrapper = JsonUtility.FromJson<QuestionListWrapper>(jsonText.text);
            domande = wrapper.domande;
        }
        else
        {
            Debug.LogError("File JSON delle domande non trovato in Resources!");
        }
    }

    public void RegistraRisposta(int valoreScelto)
    {
        if (!rispostaInAttesa) return;
        if (domande == null || domande.Count == 0 || indiceDomanda >= domande.Count) return;

        var domandaCorrente = domande[indiceDomanda];
        string testoOriginale = domandaCorrente.GetTesto(condizioneAttuale);
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");

        // ✅ Scrivi anche la scena di provenienza
        string riga = $"{idSoggetto},{condizioneAttuale},{scenaProvenienza},{domandaCorrente.id},\"{testoOriginale}\",{valoreScelto},{timestamp}";
        File.AppendAllText(logFilePath, riga + "\n");

        Debug.Log($"Risposta registrata: {riga}");

        if (testoDomanda != null)
        {
            testoDomanda.text = "";
        }

        indiceDomanda++;
        rispostaInAttesa = false;
        Invoke("MostraProssimaDomanda", 1f);
    }

    void MostraProssimaDomanda()
    {
        if (indiceDomanda < domande.Count)
        {
            string testo = domande[indiceDomanda].GetTesto(condizioneAttuale);
            testoDomanda.text = testo;
        }
        else
        {
            testoDomanda.text = "Questionario completato. Grazie!";
        }

        rispostaInAttesa = true;
    }

    void RandomizzaDomande()
    {
        domande = domande.OrderBy(x => Random.value).ToList();
    }
}
