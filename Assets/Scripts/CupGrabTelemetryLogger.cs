using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Per-frame CSV telemetry of CupSwitchDriver2Bases gate state.
/// Writes one row per Update(), so post-play analysis can see exactly why
/// pickup/dock fired (or didn't) at any moment, without recording video.
/// File: {logFolder}/CupGrabTelemetry_{timestamp}.csv
/// </summary>
public class CupGrabTelemetryLogger : MonoBehaviour
{
    public CupSwitchDriver2Bases cup;

    [Header("Output")]
    [Tooltip("Folder to write the CSV to. Created if missing.")]
    public string logFolder = @"D:\Dati Esperimenti\EO";
    [Tooltip("Optional label appended to the filename.")]
    public string fileLabel = "CupGrabTelemetry";
    [Tooltip("Flush every N rows (0 = flush every row).")]
    public int flushEvery = 30;

    [Header("Sampling")]
    [Tooltip("Skip rows when nothing has changed (state, reason, on-base, hand-in-volume identical AND speeds within tolerance).")]
    public bool skipUnchanged = true;
    [Tooltip("Speed delta (m/s) above which a row is always written even if other state is unchanged.")]
    public float speedDeltaForceLog = 0.02f;

    StreamWriter _sw;
    int _rowsSinceFlush;
    string _lastKey;
    float _lastCupSpeed, _lastAnchorSpeed;
    float _t0;
    bool _opened;

    void OnEnable()
    {
        if (cup == null) { Debug.LogWarning("[CupTelemetry] cup not assigned, disabling."); enabled = false; return; }
        OpenFile();
        _t0 = Time.time;

        cup.OnCupPickedUp += OnPicked;
        cup.OnCupDocked += OnDocked;
    }

    void OnDisable()
    {
        if (cup != null)
        {
            cup.OnCupPickedUp -= OnPicked;
            cup.OnCupDocked -= OnDocked;
        }
        CloseFile();
    }

    void OnApplicationQuit() => CloseFile();

    void OpenFile()
    {
        try
        {
            Directory.CreateDirectory(logFolder);
            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(logFolder, $"{fileLabel}_{ts}.csv");
            _sw = new StreamWriter(path, append: false, Encoding.UTF8);
            _sw.WriteLine("t,state,heldFor,onBase,handInVolume,releaseLock,cupSpeed_cms,anchorSpeed_cms,dwell,stillLimit_cms,tipDist_mm,limit_mm,closestTip,openAmount,openThr,pickupReason,dockReason,event");
            _opened = true;
            Debug.Log($"[CupTelemetry] writing to {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CupTelemetry] failed to open file: {e.Message}");
            enabled = false;
        }
    }

    void CloseFile()
    {
        if (_sw != null)
        {
            try { _sw.Flush(); _sw.Dispose(); }
            catch { /* ignore */ }
            _sw = null;
        }
        _opened = false;
    }

    void Update()
    {
        if (!_opened || cup == null) return;

        float openAmt = cup.pinchDriver != null ? cup.pinchDriver.openAmount : 0f;
        string tipName = cup.LastClosestTip != null ? cup.LastClosestTip.name : "";

        string key = string.Concat(
            cup.IsHeld ? "H" : "D", "|",
            cup.IsOnBase ? "1" : "0", "|",
            cup.HandInsideGrabVolume ? "1" : "0", "|",
            cup.LastPickupGateReason ?? "", "|",
            cup.LastDockGateReason ?? ""
        );

        bool changed = key != _lastKey;
        bool speedJump = Mathf.Abs(cup.CupSpeed - _lastCupSpeed) > speedDeltaForceLog ||
                         Mathf.Abs(cup.AnchorSpeed - _lastAnchorSpeed) > speedDeltaForceLog;

        if (skipUnchanged && !changed && !speedJump && _pendingEvent == null) return;

        WriteRow(openAmt, tipName, _pendingEvent);
        _pendingEvent = null;
        _lastKey = key;
        _lastCupSpeed = cup.CupSpeed;
        _lastAnchorSpeed = cup.AnchorSpeed;
    }

    string _pendingEvent;
    void OnPicked() { _pendingEvent = "PICKUP"; }
    void OnDocked(int b) { _pendingEvent = $"DOCK_b{b}"; }

    void WriteRow(float openAmt, string tipName, string evt)
    {
        var c = CultureInfo.InvariantCulture;
        float t = Time.time - _t0;
        _sw.WriteLine(string.Join(",",
            t.ToString("F3", c),
            cup.IsHeld ? "HELD" : "DOCKED",
            cup.HeldDuration.ToString("F3", c),
            cup.IsOnBase ? "1" : "0",
            cup.HandInsideGrabVolume ? "1" : "0",
            cup.ReleaseLocked ? "1" : "0",
            (cup.CupSpeed * 100f).ToString("F1", c),
            (cup.AnchorSpeed * 100f).ToString("F1", c),
            cup.StillTimer.ToString("F3", c),
            (cup.stillSpeed * 100f).ToString("F1", c),
            cup.LastTipDistance >= 0f ? (cup.LastTipDistance * 1000f).ToString("F1", c) : "",
            (cup.pickupContactDistance * 1000f).ToString("F1", c),
            Esc(tipName),
            openAmt.ToString("F2", c),
            cup.releaseOpenThreshold.ToString("F2", c),
            Esc(cup.LastPickupGateReason),
            Esc(cup.LastDockGateReason),
            Esc(evt ?? "")
        ));

        _rowsSinceFlush++;
        if (flushEvery <= 0 || _rowsSinceFlush >= flushEvery)
        {
            _sw.Flush();
            _rowsSinceFlush = 0;
        }
    }

    static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        bool quote = s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0;
        if (!quote) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
