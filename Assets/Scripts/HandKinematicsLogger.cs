using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Logs hand (always) + tool tip (per effector) + wrist + cup position + velocity
/// every frame to a dedicated CSV. Designed to capture the full velocity
/// profile of each trial movement.
///
/// Output CSV columns:
///   sw_ms, dt_ms, trial, phase,
///   hand_x, hand_y, hand_z,
///   vel_x, vel_y, vel_z, speed,
///   tool_x, tool_y, tool_z, tool_speed,
///   wrist_x, wrist_y, wrist_z, wrist_speed,
///   cup_x, cup_y, cup_z, cup_speed
///
/// Hand columns are the anatomical hand landmark (e.g. XRHand_IndexTip) — kept
/// constant across blocks for clean cross-effector comparisons in EEG analysis.
/// Tool columns track the effector tip (set by ReachGraspGameManager via
/// SetToolTipTransform on each effector change).
/// Wrist columns track XRHand_Wrist — same source as the runtime MOTION_ONSET
/// detector — used for offline bad-trial flagging (early motion in MotorPlanning).
///
/// Timestamps are synchronized with ExperimentLogger via the same Stopwatch.
/// </summary>
[DefaultExecutionOrder(10000)] // run after VRIK SolverManager so IK-driven bones (e.g. pinza1) are sampled post-solve
public class HandKinematicsLogger : MonoBehaviour
{
    [Header("Transforms to track")]
    [Tooltip("Anatomical hand landmark (e.g. XRHand_IndexTip). Kept constant across effectors.")]
    public Transform handTransform;

    [Tooltip("Effector tip transform (set per-effector by ReachGraspGameManager). Optional.")]
    public Transform toolTipTransform;

    [Tooltip("XRHand_Wrist (same source as runtime MOTION_ONSET). Used offline for bad-trial flagging.")]
    public Transform wristTransform;

    [Tooltip("(Optional) Cup transform for simultaneous cup tracking.")]
    public Transform cupTransform;

    [Header("Sync")]
    [Tooltip("The ExperimentLogger — used for Stopwatch sync and output folder.")]
    public ExperimentLogger logger;

    [Header("Recording")]
    [Tooltip("If true, records from Awake. If false, waits for StartRecording().")]
    public bool recordFromStart = false;

    [Tooltip("If true, only records when trial > 0 (set via SetTrialInfo). " +
             "If false, records continuously while recording is active.")]
    public bool onlyDuringTrials = true;

    [Header("File")]
    [Tooltip("Label for the output file name.")]
    public string fileLabel = "HandKinematics";

    [Tooltip("Flush every N lines (1 = every line, 10 = every 10 lines). Lower = safer, higher = faster.")]
    public int flushInterval = 5;

    // ─── Public state (set by GameManager) ──────────────────────────
    [HideInInspector] public int currentTrial = 0;
    [HideInInspector] public string currentPhase = "None";

    // ─── Internal ───────────────────────────────────────────────────
    private StreamWriter _writer;
    private Stopwatch _sw;
    private bool _recording;
    private int _linesSinceFlush;

    // velocity computation
    private Vector3 _prevHandPos;
    private Vector3 _prevToolPos;
    private Vector3 _prevWristPos;
    private Vector3 _prevCupPos;
    private bool _hasPrev;

    public string FilePath { get; private set; }

    // ─── Unity lifecycle ────────────────────────────────────────────

    void Awake()
    {
        if (recordFromStart)
            StartRecording();
    }

    void LateUpdate()
    {
        if (!_recording || _writer == null) return;
        if (handTransform == null) return;
        if (onlyDuringTrials && currentTrial <= 0) return;

        float dt = Time.deltaTime;
        float dtMs = dt * 1000f;
        long swMs = _sw != null ? _sw.ElapsedMilliseconds : (long)(Time.time * 1000);

        // ── Hand ──
        Vector3 hPos = handTransform.position;
        Vector3 hVel = Vector3.zero;
        float hSpeed = 0f;

        if (_hasPrev && dt > 1e-6f)
        {
            hVel = (hPos - _prevHandPos) / dt;
            hSpeed = hVel.magnitude;
        }
        _prevHandPos = hPos;

        // ── Tool tip (optional) ──
        Vector3 tPos = Vector3.zero;
        float tSpeed = 0f;
        if (toolTipTransform != null)
        {
            tPos = toolTipTransform.position;
            if (_hasPrev && dt > 1e-6f)
            {
                tSpeed = ((tPos - _prevToolPos) / dt).magnitude;
            }
            _prevToolPos = tPos;
        }

        // ── Wrist (optional) ──
        Vector3 wPos = Vector3.zero;
        float wSpeed = 0f;
        if (wristTransform != null)
        {
            wPos = wristTransform.position;
            if (_hasPrev && dt > 1e-6f)
            {
                wSpeed = ((wPos - _prevWristPos) / dt).magnitude;
            }
            _prevWristPos = wPos;
        }

        // ── Cup (optional) ──
        Vector3 cPos = Vector3.zero;
        float cSpeed = 0f;
        if (cupTransform != null)
        {
            cPos = cupTransform.position;
            if (_hasPrev && dt > 1e-6f)
            {
                cSpeed = ((cPos - _prevCupPos) / dt).magnitude;
            }
            _prevCupPos = cPos;
        }

        _hasPrev = true;

        // ── Write ──
        var ci = CultureInfo.InvariantCulture;

        string line = string.Join(",",
            swMs.ToString(ci),
            dtMs.ToString("F2", ci),
            currentTrial.ToString(ci),
            currentPhase,
            // hand
            hPos.x.ToString("F5", ci),
            hPos.y.ToString("F5", ci),
            hPos.z.ToString("F5", ci),
            hVel.x.ToString("F4", ci),
            hVel.y.ToString("F4", ci),
            hVel.z.ToString("F4", ci),
            hSpeed.ToString("F4", ci),
            // tool tip
            tPos.x.ToString("F5", ci),
            tPos.y.ToString("F5", ci),
            tPos.z.ToString("F5", ci),
            tSpeed.ToString("F4", ci),
            // wrist
            wPos.x.ToString("F5", ci),
            wPos.y.ToString("F5", ci),
            wPos.z.ToString("F5", ci),
            wSpeed.ToString("F4", ci),
            // cup
            cPos.x.ToString("F5", ci),
            cPos.y.ToString("F5", ci),
            cPos.z.ToString("F5", ci),
            cSpeed.ToString("F4", ci)
        );

        _writer.WriteLine(line);
        _linesSinceFlush++;

        if (_linesSinceFlush >= flushInterval)
        {
            _writer.Flush();
            _linesSinceFlush = 0;
        }
    }

    void OnApplicationQuit() => StopRecording();
    void OnDestroy() => StopRecording();

    // ─── Public API ─────────────────────────────────────────────────

    /// <summary>Called by GameManager at each phase change.</summary>
    public void SetTrialInfo(int trial, string phase)
    {
        bool changed = trial != currentTrial || phase != currentPhase;
        currentTrial = trial;
        currentPhase = phase;

        // Reset velocity history on trial/phase boundaries so the first sample
        // of a new segment doesn't produce a spurious spike against the previous
        // reference position.
        if (changed) _hasPrev = false;
    }

    /// <summary>
    /// Set the tool tip tracked when effector changes (Hand/Tweezers/TweezersGrasp).
    /// Resets velocity state to avoid a teleport artifact on the reference change.
    /// The hand transform stays constant so cross-effector kinematics share a landmark.
    /// </summary>
    public void SetToolTipTransform(Transform t)
    {
        if (t == toolTipTransform) return;
        toolTipTransform = t;
        _hasPrev = false;
    }

    public void StartRecording()
    {
        if (_recording) return;

        // Sync stopwatch
        _sw = (logger != null && logger.sw != null) ? logger.sw : Stopwatch.StartNew();

        // File path (same folder as event log)
        string dir;
        if (logger != null && !string.IsNullOrEmpty(logger.FilePath))
            dir = Path.GetDirectoryName(logger.FilePath);
        else
            dir = Path.Combine(Application.persistentDataPath, "Logs");

        Directory.CreateDirectory(dir);

        string ts = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string subj = logger != null ? logger.subjectId : "unknown";
        FilePath = Path.Combine(dir, $"{fileLabel}_{subj}_{ts}.csv");

        _writer = new StreamWriter(FilePath, false, new UTF8Encoding(false));

        // Header
        _writer.WriteLine(
            "sw_ms,dt_ms,trial,phase," +
            "hand_x,hand_y,hand_z," +
            "vel_x,vel_y,vel_z,speed," +
            "tool_x,tool_y,tool_z,tool_speed," +
            "wrist_x,wrist_y,wrist_z,wrist_speed," +
            "cup_x,cup_y,cup_z,cup_speed"
        );
        _writer.Flush();

        _hasPrev = false;
        _linesSinceFlush = 0;
        _recording = true;

        Debug.Log($"[HandKinematics] Recording started → {FilePath}");
    }

    public void StopRecording()
    {
        if (!_recording) return;
        _recording = false;

        if (_writer != null)
        {
            _writer.Flush();
            _writer.Close();
            _writer = null;
        }

        Debug.Log("[HandKinematics] Recording stopped.");
    }
}

