using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class ExperimentLogger : MonoBehaviour
{
    [Header("File")]
    public string subjectId = "S01";
    public string sessionLabel = "ReachGrasp";
    public bool flushEveryLine = true;

    [Tooltip("Folder where CSVs are saved. Leave empty to use Application.persistentDataPath/Logs.")]
    public string logFolder = @"D:\Dati Esperimenti\EO";

    private StreamWriter _writer;
    private readonly object _lock = new object();

    public Stopwatch sw { get; private set; }
    public string FilePath { get; private set; }

    void Awake()
    {
        sw = new Stopwatch();
        sw.Start();

        var rootDir = !string.IsNullOrEmpty(logFolder)
            ? logFolder
            : Path.Combine(Application.persistentDataPath, "Logs");

        // Per-subject subfolder so an experimenter can zip/export one subject at a time.
        // HandKinematicsLogger and CCT_ReachExperiment derive their folder from this.
        var dir = Path.Combine(rootDir, SafeFileName(subjectId));
        Directory.CreateDirectory(dir);

        CopyAnalysisBatches(rootDir, dir);

        var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        FilePath = Path.Combine(dir, $"{sessionLabel}_{subjectId}_{ts}.csv");

        _writer = new StreamWriter(FilePath, false, new UTF8Encoding(false));

        _writer.WriteLine(
            "pc_time_local,pc_time_utc,sw_ms,frame," +
            "trial,phase,event_name,event_value," +
            "effector," +
            "t0_panelOpen_sw_ms,trialStartBeep_sw_ms"
        );
        if (flushEveryLine) _writer.Flush();

        Debug.Log($"[LOGGER] {FilePath}");
    }

    void OnApplicationQuit() => Close();

    public void Close()
    {
        lock (_lock)
        {
            if (_writer == null) return;
            _writer.Flush();
            _writer.Close();
            _writer = null;
        }
    }

    public void Log(
        int trial,
        string phase,
        string eventName,
        string eventValue,
        string effector,
        long t0PanelOpenMs,
        long trialStartBeepMs
    )
    {
        var nowLocal = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
        var nowUtc   = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var swMs     = sw.ElapsedMilliseconds;

        string line = string.Join(",",
            Csv(nowLocal),
            Csv(nowUtc),
            swMs.ToString(CultureInfo.InvariantCulture),
            Time.frameCount.ToString(CultureInfo.InvariantCulture),

            trial.ToString(CultureInfo.InvariantCulture),
            Csv(phase),
            Csv(eventName),
            Csv(eventValue),

            Csv(effector),

            t0PanelOpenMs.ToString(CultureInfo.InvariantCulture),
            trialStartBeepMs.ToString(CultureInfo.InvariantCulture)
        );

        lock (_lock)
        {
            _writer.WriteLine(line);
            if (flushEveryLine) _writer.Flush();
        }
    }

    private static string Csv(string s)
    {
        if (s == null) return "\"\"";
        s = s.Replace("\"", "\"\"");
        return $"\"{s}\"";
    }

    private static string SafeFileName(string s)
    {
        if (string.IsNullOrEmpty(s)) return "unknown";
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }

    private static void CopyAnalysisBatches(string rootDir, string subjectDir)
    {
        try
        {
            foreach (var name in new[] {
                "run_kinematics_analysis.bat",
                "run_kinematics_average.bat",
                "run_kinematics_plot_by_effector.bat",
            })
            {
                var src = Path.Combine(rootDir, name);
                var dst = Path.Combine(subjectDir, name);
                if (File.Exists(src) && !File.Exists(dst))
                    File.Copy(src, dst);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LOGGER] Failed to copy analysis batches: {e.Message}");
        }
    }
}
