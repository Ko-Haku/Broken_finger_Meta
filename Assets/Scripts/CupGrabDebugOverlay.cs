using UnityEngine;
using System.Text;

/// <summary>
/// Runtime OnGUI overlay showing pickup/dock telemetry from CupSwitchDriver2Bases.
/// Helps diagnose: premature grabs, sticky releases, gate-blocking conditions.
/// Toggle with F1. Subscribes to OnCupPickedUp/OnCupDocked events to log last event.
/// </summary>
public class CupGrabDebugOverlay : MonoBehaviour
{
    [Tooltip("The cup driver to inspect.")]
    public CupSwitchDriver2Bases cup;
    [Tooltip("Optional: a tighter Renderer to compare its bounds to the grab collider's bounds " +
             "(diagnoses cases where the grab collider is much larger than the visible cup).")]
    public Renderer cupRenderer;

    [Header("Display")]
    public bool show = true;
    public KeyCode toggleKey = KeyCode.F1;
    public Vector2 origin = new Vector2(20f, 20f);
    public int fontSize = 14;
    public Color textColor = new Color(0.9f, 1f, 0.9f, 1f);
    public Color panelColor = new Color(0f, 0f, 0f, 0.55f);
    public float widthPx = 460f;

    string _lastEvent = "(none)";
    float _lastEventTime = -999f;
    GUIStyle _styleText, _stylePanel;

    void OnEnable()
    {
        if (cup != null)
        {
            cup.OnCupPickedUp += HandlePicked;
            cup.OnCupDocked += HandleDocked;
        }
    }

    void OnDisable()
    {
        if (cup != null)
        {
            cup.OnCupPickedUp -= HandlePicked;
            cup.OnCupDocked -= HandleDocked;
        }
    }

    void HandlePicked()
    {
        _lastEvent = "PICKUP";
        _lastEventTime = Time.time;
    }

    void HandleDocked(int which)
    {
        _lastEvent = $"DOCK base{which}";
        _lastEventTime = Time.time;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) show = !show;
    }

    void OnGUI()
    {
        if (!show || cup == null) return;
        EnsureStyles();

        var sb = new StringBuilder(1024);
        sb.AppendLine($"<b>CupGrab debug</b>  (toggle: {toggleKey})");
        sb.AppendLine($"state: <b>{(cup.IsHeld ? "HELD" : "DOCKED")}</b>" +
                      $"   onBase: {Bool(cup.IsOnBase)}" +
                      $"   handInVolume: {Bool(cup.HandInsideGrabVolume)}" +
                      $"   releaseLock: {Bool(cup.ReleaseLocked)}");
        if (cup.IsHeld) sb.AppendLine($"heldFor: {cup.HeldDuration:F2}s");
        sb.AppendLine();

        sb.AppendLine("<b>Pickup gate</b>");
        string tipName = cup.LastClosestTip != null ? cup.LastClosestTip.name : "(none)";
        string tipDistStr = cup.LastTipDistance >= 0f ? $"{cup.LastTipDistance * 1000f:F1} mm" : "n/a";
        sb.AppendLine($"  tipDist: {tipDistStr}   limit: {cup.pickupContactDistance * 1000f:F1} mm");
        sb.AppendLine($"  closestTip: {tipName}");
        sb.AppendLine($"  reason: {cup.LastPickupGateReason}");
        sb.AppendLine();

        sb.AppendLine("<b>Dock gate</b> (release)");
        sb.AppendLine($"  cupSpeed: {cup.CupSpeed * 100f:F1} cm/s   anchor: {cup.AnchorSpeed * 100f:F1} cm/s   limit: {cup.stillSpeed * 100f:F1} cm/s");
        sb.AppendLine($"  dwell: {cup.StillTimer:F2}/{cup.stillDwellTime:F2} s");
        float openAmt = cup.pinchDriver != null ? cup.pinchDriver.openAmount : 0f;
        sb.AppendLine($"  openAmount: {openAmt:F2}   threshold: {cup.releaseOpenThreshold:F2}");
        sb.AppendLine($"  reason: {cup.LastDockGateReason}");
        sb.AppendLine();

        sb.AppendLine("<b>Geometry</b>");
        var grabCol = cup.ResolvedGrabCollider;
        if (grabCol != null)
        {
            Vector3 gs = grabCol.bounds.size;
            sb.AppendLine($"  grabCollider bounds: {gs.x * 100f:F1} × {gs.y * 100f:F1} × {gs.z * 100f:F1} cm");
        }
        if (cupRenderer != null)
        {
            Vector3 rs = cupRenderer.bounds.size;
            sb.AppendLine($"  renderer  bounds: {rs.x * 100f:F1} × {rs.y * 100f:F1} × {rs.z * 100f:F1} cm");
        }
        sb.AppendLine();

        float since = Time.time - _lastEventTime;
        string sinceStr = _lastEventTime < 0f ? "—" : $"{since:F1}s ago";
        sb.AppendLine($"<b>Last event</b>: {_lastEvent}   ({sinceStr})");

        string text = sb.ToString();
        Vector2 size = _styleText.CalcSize(new GUIContent(text));
        float w = Mathf.Max(widthPx, size.x + 24f);
        float h = _styleText.CalcHeight(new GUIContent(text), w - 24f) + 16f;

        Rect panel = new Rect(origin.x, origin.y, w, h);
        GUI.Box(panel, GUIContent.none, _stylePanel);
        GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, panel.height - 16f), text, _styleText);
    }

    static string Bool(bool v) => v ? "<color=#80ff80>YES</color>" : "<color=#ff8080>no</color>";

    void EnsureStyles()
    {
        if (_styleText != null) return;
        _styleText = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            richText = true,
            wordWrap = false,
            normal = { textColor = textColor }
        };
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, panelColor);
        tex.Apply();
        _stylePanel = new GUIStyle(GUI.skin.box)
        {
            normal = { background = tex }
        };
    }
}
