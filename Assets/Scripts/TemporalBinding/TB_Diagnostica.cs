using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Log diagnostico per il pilota:
//  - console: una riga solo quando qualcosa cambia stato (mano persa, braccio non disegnato, swap...)
//  - CSV a 'hz' campioni/s in Reports/TemporalBinding/Diagnostica_*.csv
public class TB_Diagnostica : MonoBehaviour
{
    public GameObject[] avatar;
    [Tooltip("Mesh da controllare se vengono disegnate (bracci LM, moncherino)")]
    public Renderer[] renderers;
    public TB_CalibrazioneCorpo calibrazione;
    public float hz = 10f;
    public bool scriviCsv = true;

    TB_ManoRealistica[] mani;
    readonly Dictionary<string, string> stato = new Dictionary<string, string>();
    StreamWriter csv;
    float prossimo;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    void Start()
    {
        var l = new List<TB_ManoRealistica>();
        foreach (var a in avatar) if (a) l.AddRange(a.GetComponentsInChildren<TB_ManoRealistica>(true));
        mani = l.ToArray();

        if (!scriviCsv) return;
        string dir = Application.isEditor
            ? Path.Combine(Application.dataPath, "Reports", "TemporalBinding")
            : Path.Combine(Application.persistentDataPath, "TemporalBinding");
        Directory.CreateDirectory(dir);
        csv = new StreamWriter(Path.Combine(dir, $"Diagnostica_{DateTime.Now:yyyyMMdd_HHmmss}.csv"));
        var h = new StringBuilder("t,frame,fps");
        foreach (var a in avatar) h.Append($",{a.name}_attivo");
        foreach (var m in mani) { string n = Nome(m); h.Append($",{n}_sorgente,{n}_conf,{n}_calibrata,{n}_x,{n}_y,{n}_z"); }
        foreach (var r in renderers) h.Append($",{r.name}_{r.transform.parent.name}_visibile");
        csv.WriteLine(h);
    }

    static string Nome(TB_ManoRealistica m) => $"{m.name}_{(m.polso.name.Contains("Right") ? "dx" : "sx")}";

    void LateUpdate()
    {
        // eventi: cambi di stato in console
        foreach (var a in avatar) Cambio(a.name, a.activeInHierarchy ? "attivo" : "spento");
        foreach (var m in mani)
        {
            if (!m.isActiveAndEnabled) continue;
            Cambio(Nome(m) + " sorgente", m.Sorgente);
            Cambio(Nome(m) + " calibrata", m.Calibrata ? "sì" : "no");
        }
        foreach (var r in renderers)
            if (r && r.gameObject.activeInHierarchy) Cambio(r.transform.parent.name + "/" + r.name, r.isVisible ? "disegnato" : "NON disegnato");
        if (calibrazione) Cambio("calibrazione corpo", calibrazione.Calibrato ? "fatta" : "non fatta");

        if (csv == null || Time.unscaledTime < prossimo) return;
        prossimo = Time.unscaledTime + 1f / hz;
        var s = new StringBuilder();
        s.Append(Time.unscaledTime.ToString("F3", Inv)).Append(',').Append(Time.frameCount).Append(',')
         .Append((1f / Mathf.Max(1e-4f, Time.unscaledDeltaTime)).ToString("F0", Inv));
        foreach (var a in avatar) s.Append(',').Append(a.activeInHierarchy ? 1 : 0);
        foreach (var m in mani)
        {
            bool on = m.isActiveAndEnabled;
            Vector3 p = m.polso.position;
            s.Append(',').Append(on ? m.Sorgente : "-").Append(',').Append(on && m.AltaConfidenza ? 1 : 0)
             .Append(',').Append(m.Calibrata ? 1 : 0)
             .Append(',').Append(p.x.ToString("F3", Inv)).Append(',').Append(p.y.ToString("F3", Inv)).Append(',').Append(p.z.ToString("F3", Inv));
        }
        foreach (var r in renderers) s.Append(',').Append(r && r.gameObject.activeInHierarchy && r.isVisible ? 1 : 0);
        csv.WriteLine(s);
    }

    void Cambio(string chiave, string valore)
    {
        if (stato.TryGetValue(chiave, out var prima) && prima == valore) return;
        stato[chiave] = valore;
        Debug.Log($"[TB diag] t={Time.unscaledTime:F2}s {chiave}: {(prima ?? "?")} -> {valore}");
    }

    void OnDestroy() { csv?.Flush(); csv?.Dispose(); csv = null; }
}
