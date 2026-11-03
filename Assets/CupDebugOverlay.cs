using UnityEngine;

/// <summary>
/// Attach this to the Cup GameObject.
/// Live debug overlay for:
/// - Transform-delta velocity (always meaningful)
/// - Rigidbody velocity (often zero when kinematic/parented)
/// - Base proximity
/// - Pickup heuristic based on moved distance from a dock reference position (ANY direction)
/// - Dock heuristic based on onBase + speed < stillSpeed (immediate)
///
/// NOTE:
/// If your main driver sets rb.isKinematic=true and uses parenting, rb.linearVelocity may stay ~0.
/// </summary>
public class CupDebugOverlay : MonoBehaviour
{
    [Header("References (optional)")]
    public Rigidbody rb;
    public Transform base1;
    public Transform base2;

    [Header("Base detection (match your main script values)")]
    public float baseRadius = 0.10f;

    [Header("Pickup detection (match your main script values)")]
    public float pickupMoveDistance = 0.01f; // 1 cm

    [Header("Release detection (match your main script values)")]
    public float stillSpeed = 0.03f;

    [Header("Speed source to compare")]
    [Tooltip("If true, heuristic 'speed' uses rb.linearVelocity. If false, uses transform-delta velocity.")]
    public bool useRigidbodyVelocityForSpeed = false;

    [Header("Dock reference capture")]
    [Tooltip("If true, capture dock reference position at Awake.")]
    public bool captureDockRefAtStart = true;
    [Tooltip("Key to recapture dock reference world position (useful right after a docking).")]
    public KeyCode recaptureDockRefKey = KeyCode.R;

    [Header("Debug")]
    public bool showOnScreen = true;
    public bool logOnStateChange = true;
    public int fontSize = 22;
    public bool logOncePerSecond = false;

    // Derived velocities
    private Vector3 _prevPos;
    private Vector3 _velFromTransform;

    // Dock reference (world position) used for pickup movement heuristic
    private Vector3 _dockRefWorldPos;

    // Heuristic signals (doesn't control anything)
    private bool _wouldPickup;
    private bool _wouldDock;

    // For change logging
    private bool _prevWouldPickup;
    private bool _prevWouldDock;

    private float _secTimer;

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        _prevPos = transform.position;

        if (captureDockRefAtStart)
            _dockRefWorldPos = transform.position;
    }

    void Update()
    {
        // Recapture dock reference (manual)
        if (Input.GetKeyDown(recaptureDockRefKey))
        {
            _dockRefWorldPos = transform.position;
            Debug.Log($"[CupDebug] DockRef recaptured at worldPos={_dockRefWorldPos}");
        }

        // Transform-delta velocity (works even if rb velocity is zero)
        float dt = Mathf.Max(Time.deltaTime, 1e-6f);
        _velFromTransform = (transform.position - _prevPos) / dt;
        _prevPos = transform.position;

        // RB velocity (may be unreliable if kinematic/parented)
        Vector3 rbVel = rb ? rb.linearVelocity : Vector3.zero;

        // Choose a speed source for "still" heuristic
        Vector3 velChosen = _velFromTransform;
        if (useRigidbodyVelocityForSpeed && rb != null)
            velChosen = rbVel;

        float speedT = _velFromTransform.magnitude;
        float speedRB = rbVel.magnitude;
        float speedChosen = velChosen.magnitude;

        bool onBase = IsOnAnyBase();
        int which = WhichBase();

        // Pickup heuristic (ANY direction): moved >= pickupMoveDistance
        float movedFromDock = Vector3.Distance(transform.position, _dockRefWorldPos);
        _wouldPickup = movedFromDock >= pickupMoveDistance;

        // Dock heuristic (immediate): onBase + speed < stillSpeed
        _wouldDock = onBase && (speedChosen < stillSpeed);

        if (logOnStateChange)
        {
            if (_wouldPickup != _prevWouldPickup)
            {
                Debug.Log($"[CupDebug] wouldPickup -> {_wouldPickup} | moved={movedFromDock:F4}/{pickupMoveDistance:F4}");
                _prevWouldPickup = _wouldPickup;
            }

            if (_wouldDock != _prevWouldDock)
            {
                Debug.Log($"[CupDebug] wouldDock -> {_wouldDock} | onBase={onBase} which={which} speed={speedChosen:F4} < {stillSpeed:F4} (src={(useRigidbodyVelocityForSpeed ? "RB" : "TR")})");
                _prevWouldDock = _wouldDock;
            }
        }

        if (logOncePerSecond)
        {
            _secTimer += Time.deltaTime;
            if (_secTimer >= 1f)
            {
                _secTimer = 0f;
                Debug.Log(ToText(speedT, speedRB, speedChosen, movedFromDock, onBase, which));
            }
        }
    }

    bool IsOnAnyBase()
    {
        return IsNear(base1) || IsNear(base2);
    }

    bool IsNear(Transform t)
    {
        if (!t) return false;
        return Vector3.Distance(transform.position, t.position) <= baseRadius;
    }

    int WhichBase()
    {
        bool on1 = IsNear(base1);
        bool on2 = IsNear(base2);
        if (on1 && !on2) return 1;
        if (on2 && !on1) return 2;
        if (on1 && on2) return 3;
        return 0;
    }

    string ToText(float speedT, float speedRB, float speedChosen, float movedFromDock, bool onBase, int which)
    {
        float d1 = base1 ? Vector3.Distance(transform.position, base1.position) : -1f;
        float d2 = base2 ? Vector3.Distance(transform.position, base2.position) : -1f;

        string rbInfo = "no rb";
        if (rb)
        {
            rbInfo =
                $"rb.isKinematic={rb.isKinematic} " +
                $"rb.linVel={rb.linearVelocity} mag={rb.linearVelocity.magnitude:F4} " +
                $"rb.angVelMag={rb.angularVelocity.magnitude:F4}";
        }

        return
            $"CupDebug\n" +
            $"pos: {transform.position}\n" +
            $"dockRef(world): {_dockRefWorldPos}  (press '{recaptureDockRefKey}' to recapture)\n" +
            $"movedFromDock: {movedFromDock:F4} / {pickupMoveDistance:F4}  -> wouldPickup={_wouldPickup}\n" +
            $"vel(TR): {_velFromTransform}  speedT={speedT:F4}\n" +
            $"vel(RB): {(rb ? rb.linearVelocity.ToString() : "no rb")}  speedRB={speedRB:F4}\n" +
            $"speedChosen={speedChosen:F4} (src={(useRigidbodyVelocityForSpeed ? "RB" : "TR")})  still<{stillSpeed:F4}\n" +
            $"dist base1={d1:F3} dist base2={d2:F3} baseRadius={baseRadius:F3}\n" +
            $"onBase={onBase} which={which} -> wouldDock={_wouldDock}\n" +
            $"{rbInfo}\n";
    }

    void OnGUI()
    {
        if (!showOnScreen) return;

        float speedT = _velFromTransform.magnitude;
        float speedRB = rb ? rb.linearVelocity.magnitude : 0f;

        Vector3 velChosen = _velFromTransform;
        if (useRigidbodyVelocityForSpeed && rb != null)
            velChosen = rb.linearVelocity;

        float speedChosen = velChosen.magnitude;

        bool onBase = IsOnAnyBase();
        int which = WhichBase();

        float movedFromDock = Vector3.Distance(transform.position, _dockRefWorldPos);

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = fontSize;
        style.normal.textColor = Color.white;

        string text = ToText(speedT, speedRB, speedChosen, movedFromDock, onBase, which);
        GUI.Label(new Rect(20, 20, 1400, 900), text, style);
    }
}
