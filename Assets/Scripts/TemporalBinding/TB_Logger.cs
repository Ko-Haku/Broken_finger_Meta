using System;
using System.Globalization;
using System.IO;
using UnityEngine;

// Un CSV per sessione, una riga per trial, scritta subito (niente perdita dati se crasha).
// Il questionario di fine blocco va in un secondo CSV con lo stesso nome + "_questionario", una riga per item.
public class TB_Logger : MonoBehaviour
{
    string filePath;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public string FilePath => filePath;

    public void Apri(string soggetto, string numero, string ordineBlocchi)
    {
        string dir = Application.isEditor
            ? Path.Combine(Application.dataPath, "Reports", "TemporalBinding")
            : Path.Combine(Application.persistentDataPath, "TemporalBinding");
        Directory.CreateDirectory(dir);
        filePath = Path.Combine(dir, $"TB_{soggetto}_{numero}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        File.WriteAllText(filePath,
            "Soggetto,Numero,OrdineBlocchi,Blocco,Condizione,Effettore,Agency,Trial,Warmup," +
            "DelayNominale_ms,DelayEffettivo_ms,Stima_ms,SliderStart,RT_risposta_ms,Latenza_azione_ms," +
            "MovimentoManoReale_mm,ClipPassivo,FrameRate,Refresh_Hz,FramePersi,Ripetizione,Timestamp\n");
        soggettoId = soggetto; numeroId = numero; ordine = ordineBlocchi;
        fileQuestionario = Path.Combine(dir, Path.GetFileNameWithoutExtension(filePath) + "_questionario.csv");
        File.WriteAllText(fileQuestionario,
            "Soggetto,Numero,OrdineBlocchi,Blocco,Condizione,Effettore,Agency,Item,Posizione,Risposta,SliderStart,RT_ms,Testo,Timestamp\n");
    }

    string soggettoId, numeroId, ordine, fileQuestionario;
    public string FileQuestionario => fileQuestionario;

    public void ScriviItem(int blocco, TB_Condizione c, string item, int posizione, int risposta, int sliderStart, double rtMs, string testo)
    {
        if (fileQuestionario == null) return;
        string riga = string.Join(",",
            soggettoId, numeroId, ordine, blocco, c.Nome, c.effettore, c.agency,
            item, posizione, risposta, sliderStart, rtMs.ToString("F0", Inv),
            "\"" + testo.Replace("\"", "'") + "\"",
            DateTime.Now.ToString("HH:mm:ss.fff"));
        File.AppendAllText(fileQuestionario, riga + "\n");
    }

    public void Scrivi(TB_Trial t)
    {
        if (filePath == null) return;
        string riga = string.Join(",",
            soggettoId, numeroId, ordine,
            t.blocco, t.condizione.Nome, t.condizione.effettore, t.condizione.agency,
            t.indice, t.warmup ? 1 : 0,
            t.delayNominaleMs,
            t.DelayEffettivoMs.ToString("F1", Inv),
            t.stimaMs, t.sliderStart,
            t.RtRispostaMs.ToString("F0", Inv),
            t.LatenzaAzioneMs.ToString("F1", Inv),
            t.movimentoManoRealeMm.ToString("F1", Inv),
            t.clipIndex,
            (1f / Mathf.Max(1e-4f, Time.smoothDeltaTime)).ToString("F0", Inv),
            (TB_Timing.Instance ? TB_Timing.Instance.FrequenzaHz : 0f).ToString("F1", Inv),
            t.framePersi, t.ripetizione,
            DateTime.Now.ToString("HH:mm:ss.fff"));
        File.AppendAllText(filePath, riga + "\n");
    }
}
