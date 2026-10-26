using System;
using System.Globalization;
using System.IO;
using UnityEngine;

// Un CSV per sessione, una riga per trial, scritta subito (niente perdita dati se crasha).
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
            "MovimentoManoReale_mm,ClipPassivo,FrameRate,Timestamp\n");
        soggettoId = soggetto; numeroId = numero; ordine = ordineBlocchi;
    }

    string soggettoId, numeroId, ordine;

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
            DateTime.Now.ToString("HH:mm:ss.fff"));
        File.AppendAllText(filePath, riga + "\n");
    }
}
