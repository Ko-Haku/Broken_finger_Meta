using System;
using UnityEngine;

/// <summary>
/// Cup driver switch (2 states):
/// - Docked: cup sits at scene root; Update mirrors joystickAnchor world pose.
///           If the anchor hierarchy is inactive (XR tracking loss) the cup
///           freezes at its last pose instead of disappearing.
/// - Held:   cup is parented to handGripPoint (hand tracking)
///
/// PICKUP (Docked -> Held), default mode (pickupOnGrabVolumeEnter=true):
///   fires on CupGrabVolume trigger ENTER — effector must be touching the cup.
/// Legacy mode (pickupOnGrabVolumeEnter=false): fires on motion triggers
///   (lift/move/rotate) gated by hand proximity.
///
/// RELEASE (Held -> Docked) triggers on either:
///   B) Cup still (speed < stillSpeed) for stillDwellTime while on base
///   C) Cup enters a base zone while held (autoDockOnBaseTouch, primary)
///
/// Once HELD, the cup stays attached until it's physically returned to a base.
/// Finger-aperture is no longer used for release: openAmount measures
/// thumb-to-index distance, which stays high while the hand wraps an object,
/// causing false releases when the hand momentarily exited the grab volume.
///
/// External disable (used by CCT_ReachExperiment): set pickupMoveDistance >= 1f
/// (legacy) or pickupOnGrabVolumeEnter = false to block pickup entirely.
/// </summary>
public class CupSwitchDriver2Bases : MonoBehaviour
{
    [Header("Anchors")]
    public Transform handGripPoint;      // parenting target when held + proximity check
    public Transform joystickAnchor;     // stable controller/joystick anchor (parent when docked)

    [Header("Bases (2)")]
    public Transform base1;
    public Transform base2;
    [Tooltip("Max HORIZONTAL (XZ) distance from base center to consider the cup 'on that base'.")]
    public float baseRadius = 0.10f;
    [Tooltip("Hysteresis: once on base, cup must exceed this XZ distance to leave.")]
    public float baseExitRadius = 0.13f;
    [Tooltip("Max VERTICAL (|ΔY|) distance between cup pivot and base position (m). " +
             "Default 0.15 m accommodates cup half-height (~7 cm) + tolerance. " +
             "Distance is checked separately from baseRadius — the cup must be over the base horizontally AND not too far above/below it.")]
    public float baseYTolerance = 0.15f;

    [Header("Mode")]
    public bool startDocked = true;

    [Header("Mirror gate (optional)")]
    [Tooltip("When assigned, the world-pose mirror runs ONLY while this GameObject is activeInHierarchy. " +
             "Use to disable the mirror when the left hand switches from controller (joystick) to hand-tracking. " +
             "Typical assignment: the controller mesh (oculus_controller_l_MeshX) or its parent (MetaQuestTouchPlus_Left), " +
             "which Meta's multimodal SDK deactivates during hand-tracking. Leave null to keep previous behavior.")]
    public Transform controllerActiveProbe;

    [Header("Pickup mode")]
    [Tooltip("If true, pickup fires when the hand is inside the grab volume AND (if gripperBlend is assigned) tip-contact has been reached. Ignores motion-based triggers below.")]
    public bool pickupOnGrabVolumeEnter = true;

    [Tooltip("External kill-switch: when true, TryPickup returns immediately and the cup cannot be grabbed. " +
             "Used by CCT_ReachExperiment during reach-only trials to prevent accidental grabs.")]
    public bool pickupDisabled = false;

    [Tooltip("If true, the cup auto-docks the moment it enters a base zone while held. " +
             "After auto-dock the effector must exit the grab volume and re-enter to allow another pickup. " +
             "Path B (still-on-base) remains as a fallback.")]
    public bool autoDockOnBaseTouch = true;

    [Header("Tip-contact gate (anti-premature grab)")]
    [Tooltip("Effector tips (XRHand_IndexTip, pinza1_tip, ...) that must reach the cup before pickup fires. " +
             "Pickup is held back until the closest assigned tip is within pickupContactDistance of the cup's trigger collider. " +
             "Leave empty to fall back to volume-enter-only (legacy: cup is grabbed as soon as ANY hand collider enters the grab volume).")]
    public Transform[] effectorTips;
    [Tooltip("Required tip-to-cup distance (m). Pickup fires when the nearest effectorTip is within this distance " +
             "of the cup's trigger collider surface. Default 0.015 = 1.5 cm — small but tolerant of tracking jitter.")]
    public float pickupContactDistance = 0.015f;
    [Tooltip("If true, ALL active tips in effectorTips[] must be within pickupContactDistance for pickup to fire. " +
             "Use this for pollice+indice grip semantics (both fingers must touch the cup). " +
             "If false (legacy), pickup fires as soon as the NEAREST tip is in contact.")]
    public bool pickupRequireAllActiveTips = false;
    [Tooltip("Optional explicit cup grab collider used by the contact gate. If null, auto-resolves to the trigger collider on this GameObject.")]
    public Collider grabCollider;
    private Collider _resolvedGrabCollider;

    [Header("Pickup triggers (legacy, only if pickupOnGrabVolumeEnter=false)")]
    [Tooltip("Vertical lift from dock-time Y that triggers pickup. 0.001 = 1 mm (primary trigger).")]
    public float pickupLiftDistance = 0.001f;

    [Tooltip("Anchor displacement from dock ref that triggers pickup. Set >= 1f to disable ALL pickup.")]
    public float pickupMoveDistance = 0.02f;

    [Tooltip("Anchor rotation from dock ref (deg) that triggers pickup.")]
    public float pickupRotateDegrees = 15f;

    [Header("Pickup gate (hand must be close) — legacy")]
    [Tooltip("Fallback distance when hand is not inside grab volume.")]
    public float handNearDistance = 0.06f;

    [Header("Release detection")]
    [Tooltip("Cup speed threshold (m/s) below which the cup is considered still. " +
             "Measured on the held cup's own transform — semantically: the cup is no longer moving.")]
    public float stillSpeed = 0.06f;
    [Tooltip("Time (s) the cup must stay below stillSpeed while on base to dock.")]
    public float stillDwellTime = 0.08f;

    [Header("Pinch readout (informational, not used for release)")]
    [Tooltip("Informational only — not used for release gating. Telemetry/overlay read openAmount for diagnostics.")]
    public PinchOpenDriver_Interaction pinchDriver;
    [Tooltip("Informational only — not used for release gating. Kept for telemetry/overlay display.")]
    [Range(0f, 1f)]
    public float releaseOpenThreshold = 0.7f;

    [Header("Anti re-grab / re-dock")]
    [Tooltip("After pickup, docking is blocked for this duration.")]
    public float minHeldTimeBeforeDock = 0.25f;
    [Tooltip("After dock, pickup is blocked for this duration.")]
    public float releaseCooldown = 0.35f;
    [Tooltip("After pickup, the cup must move at least this far (m) from the pickup pose " +
             "before auto-on-base / stillness paths can re-dock. Default 0.03 (3 cm) — " +
             "above tracking jitter, below any real reach motion. " +
             "Robust to close/overlapping base zones (works regardless of baseRadius geometry).")]
    public float armMoveDistance = 0.03f;

    [Header("Dock correction (continuous settle)")]
    [Tooltip("Exponential decay speed toward dock pose while docked. 0 = off.")]
    public float dockCorrectionSpeed = 4f;
    [Tooltip("Stop correcting when offset below this (meters).")]
    public float dockCorrectionDeadZone = 0.0005f;

    [Header("Debug")]
    public bool debugLogs = true;
    public float debugLogRate = 1f;

    // Events
    public event Action OnCupPickedUp;        // Docked -> Held
    public event Action<int> OnCupDocked;     // Held -> Docked (whichBase: 0/1/2/3)

    // ─────────── Internal state ───────────
    private bool _heldByHand;

    // dock-time references (frozen while docked; refreshed when hand is far)
    private Vector3 _dockRefAnchorPos;
    private Quaternion _dockRefAnchorRot;
    private float _dockRefCupY;

    // dock local pose (for settle correction under joystickAnchor)
    private Vector3 _dockLocalPos;
    private Quaternion _dockLocalRot;
    private Vector3 _dockLocalScale;
    private bool _dockPoseCaptured;
    private bool _dockCorrectionActive;

    // anchor velocity (for docked-state diagnostics)
    private Vector3 _prevAnchorPos;
    private float _anchorSpeed;

    // cup velocity (used for release stillness detection while held)
    private Vector3 _prevCupPos;
    private float _cupSpeed;

    // grab volume membership (from CupGrabVolume.SetHandInside)
    private bool _handInsideGrabVolume;

    // re-pickup arming: after a dock the effector must EXIT and RE-ENTER the
    // grab volume before another pickup can fire. Prevents the cup re-grabbing
    // itself the moment the auto-dock-on-base lets go while the hand is still inside.
    private bool _armedForPickup = true;

    // anchor tracking-active state (to detect tracking-lost→resumed transitions)
    private bool _anchorWasActive = true;

    // base hysteresis
    private bool _isOnBase;

    // anti immediate-redock: after pickup, the cup must leave the base zone(s)
    // it was sitting on at pickup time before auto-on-base / stillness paths
    // can fire. Prevents the "touch cup → 150 ms → re-dock → cup never reaches
    // the hand" pattern when the user grabs without lifting out of the base zone,
    // and also covers the case where two bases are close together and the cup
    // moves between them without ever leaving the union of the two zones.
    private bool _cupExitedBaseSincePickup;
    private bool _pickupNearBase1;
    private bool _pickupNearBase2;
    private Vector3 _cupPosAtPickup;

    // locks / timers
    private bool _releaseLock;
    private float _releaseUnlockTime;
    private float _heldSinceTime = -999f;
    private float _stillTimer;

    // debug timer
    private float _dbgTimer;

    // ─────────── Lifecycle ───────────

    private void Awake()
    {
        _prevAnchorPos = joystickAnchor != null ? joystickAnchor.position : Vector3.zero;
        _prevCupPos = transform.position;

        if (startDocked)
        {
            _heldByHand = false;
            // Capture dock pose (relative to anchor) from editor-time parenting,
            // then unparent to scene root so the cup stays visible if the XR
            // anchor hierarchy goes inactive (tracking loss).
            CaptureDockPoseIfNeeded();
            if (!_dockPoseCaptured) CaptureDockPoseFromWorld();
            transform.SetParent(null, worldPositionStays: true);
            ApplyDockWorldPose();
            SnapshotDockRefs();
            Debug.Log("[Cup] START -> DOCKED (world-pose mirror)");
        }
        else
        {
            _heldByHand = true;
            _heldSinceTime = Time.time;
            ParentTo(handGripPoint);
            Debug.Log("[Cup] START -> HELD");
        }
    }

    /// <summary>True when a hand/gripper collider is inside CupGrabVolume's trigger.</summary>
    public bool HandInsideGrabVolume => _handInsideGrabVolume;

    // ─── Diagnostics (read-only, for debug overlay) ────────────────────
    public bool IsHeld => _heldByHand;
    public bool IsOnBase => _isOnBase;
    public float CupSpeed => _cupSpeed;
    public float AnchorSpeed => _anchorSpeed;
    public float StillTimer => _stillTimer;
    public bool ReleaseLocked => _releaseLock;
    public bool ArmedForPickup => _armedForPickup;
    public float HeldDuration => _heldByHand ? Time.time - _heldSinceTime : 0f;
    public float LastTipDistance => _lastTipDist;          // m, -1 if not computed
    public Transform LastClosestTip => _lastClosestTip;    // which tip is closest
    public string LastPickupGateReason => _lastGateReason; // why pickup is blocked (or fired)
    public string LastDockGateReason => _lastDockReason;   // why dock is blocked (or fired)
    public Collider ResolvedGrabCollider => _resolvedGrabCollider;

    // diagnostic fields populated by gate logic
    private float _lastTipDist = -1f;
    private Transform _lastClosestTip;
    private string _lastGateReason = "(idle)";
    private string _lastDockReason = "(idle)";

    /// <summary>Called by CupGrabVolume trigger.</summary>
    public void SetHandInside(bool inside)
    {
        _handInsideGrabVolume = inside;
        if (debugLogs) Debug.Log($"[Cup] handInsideGrabVolume -> {inside}");

        // Re-arm pickup the moment the effector LEAVES the grab volume.
        // Combined with disarm-on-dock, this enforces the exit→re-enter requirement.
        if (!inside) _armedForPickup = true;
    }

    private bool _frozen = false;
    public bool IsFrozen => _frozen;

    /// Stops all cup movement and unparents from joystick anchor (so hand-tracking
    /// takeover on the left controller can't drag the cup). Re-enables + reparents
    /// when set back to false. Used by CCT_ReachExperiment.
    public void SetFrozen(bool frozen)
    {
        if (_frozen == frozen) return;
        _frozen = frozen;

        if (frozen)
        {
            transform.SetParent(null, worldPositionStays: true);
            enabled = false;
            Debug.Log("[Cup] FROZEN (tracking disabled, unparented)");
        }
        else
        {
            // Stay at scene root; Update() will mirror anchor world pose.
            enabled = true;
            Debug.Log("[Cup] UNFROZEN (world-pose mirror)");
        }
    }

    private void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 1e-6f);

        UpdateAnchorVelocity(dt);
        UpdateReleaseLock();

        if (!_heldByHand)
        {
            RefreshDockRefsIfHandFar();
            UpdateDockedWorldPose(dt);
            TryPickup();
        }
        else
        {
            TryDock();
        }
    }

    // ─────────── Pickup logic ───────────

    private void TryPickup()
    {
        if (pickupDisabled) { _lastGateReason = "ext-disabled"; return; }
        if (_releaseLock) { _lastGateReason = "release-lock"; return; }
        if (!_armedForPickup) { _lastGateReason = "needs hand-exit→reenter"; return; }

        // Modern path: hand inside grab-volume + (optional) tip-contact gate.
        if (pickupOnGrabVolumeEnter)
        {
            if (!_handInsideGrabVolume) { _lastGateReason = "no hand in volume"; return; }

            float tipDist = -1f;
            if (effectorTips != null && effectorTips.Length > 0)
            {
                tipDist = MinEffectorTipDistanceToCup();
                if (tipDist < 0f || tipDist > pickupContactDistance)
                {
                    _lastGateReason = $"tipDist {tipDist*1000f:F1}mm > {pickupContactDistance*1000f:F1}mm";
                    if (debugLogs)
                    {
                        _dbgTimer += Time.deltaTime;
                        if (_dbgTimer >= debugLogRate)
                        {
                            _dbgTimer = 0f;
                            Debug.Log($"[CupPickupDBG] inside=TRUE tipDist={tipDist*1000f:F1}mm > limit={pickupContactDistance*1000f:F1}mm (waiting contact)");
                        }
                    }
                    return;
                }
            }
            _lastGateReason = $"FIRED tipDist={(tipDist >= 0f ? (tipDist*1000f).ToString("F1")+"mm" : "n/a")}";
            Debug.Log($"[Cup] PICKUP (volume+contact tipDist={(tipDist >= 0f ? (tipDist*1000f).ToString("F1")+"mm" : "n/a")})");
            SnapToHand();
            return;
        }

        // External disable sentinel (CCT_ReachExperiment sets this to 999 during reach-only)
        if (pickupMoveDistance >= 1f) return;

        // Gate: hand must be close (volume or distance)
        bool handClose = _handInsideGrabVolume || EffectorDistanceToCup() <= handNearDistance;
        if (!handClose) return;

        // Triggers (any-of)
        float lift = transform.position.y - _dockRefCupY;
        float moved = joystickAnchor != null
            ? Vector3.Distance(joystickAnchor.position, _dockRefAnchorPos)
            : 0f;
        float rotDeg = joystickAnchor != null
            ? Quaternion.Angle(joystickAnchor.rotation, _dockRefAnchorRot)
            : 0f;

        bool liftTrig = lift >= pickupLiftDistance;
        bool moveTrig = moved >= pickupMoveDistance;
        bool rotTrig = rotDeg >= pickupRotateDegrees;

        if (debugLogs)
        {
            _dbgTimer += Time.deltaTime;
            if (_dbgTimer >= debugLogRate)
            {
                _dbgTimer = 0f;
                Debug.Log(
                    $"[CupPickupDBG] lift={lift*1000:F1}mm/{pickupLiftDistance*1000:F1}mm " +
                    $"moved={moved:F4}/{pickupMoveDistance:F4} rotDeg={rotDeg:F1}/{pickupRotateDegrees:F1} " +
                    $"handClose={handClose} inside={_handInsideGrabVolume}"
                );
            }
        }

        if (liftTrig || moveTrig || rotTrig)
        {
            Debug.Log($"[Cup] PICKUP | lift={lift*1000:F1}mm moved={moved:F4} rotDeg={rotDeg:F1}");
            SnapToHand();
        }
    }

    // ─────────── Release logic ───────────

    private void TryDock()
    {
        bool rawOnBase = IsOnAnyBase(baseRadius);
        if (!_isOnBase && rawOnBase) _isOnBase = true;
        else if (_isOnBase && !IsOnAnyBase(Mathf.Max(baseExitRadius, baseRadius))) _isOnBase = false;

        // Arm the auto-dock paths via two OR-ed signals:
        //  (a) cup has moved at least armMoveDistance from the pickup pose — geometry-free,
        //      robust to overlapping/close base zones (the user's own motion is the gate)
        //  (b) cup has left the base zone(s) it was on at pickup time — semantic check that
        //      complements (a) when bases are well separated
        if (!_cupExitedBaseSincePickup)
        {
            float movedFromPickup = Vector3.Distance(transform.position, _cupPosAtPickup);
            bool stillNearPickupBase = (_pickupNearBase1 && IsNearBase(base1, Mathf.Max(baseExitRadius, baseRadius)))
                                    || (_pickupNearBase2 && IsNearBase(base2, Mathf.Max(baseExitRadius, baseRadius)));
            if (movedFromPickup > armMoveDistance || !stillNearPickupBase) _cupExitedBaseSincePickup = true;
        }

        bool dockAllowedByTime = (Time.time - _heldSinceTime) >= minHeldTimeBeforeDock;

        // Path C (primary): auto-dock as soon as the cup enters a base zone.
        // Re-pickup requires the effector to exit + re-enter the grab volume.
        if (autoDockOnBaseTouch && dockAllowedByTime && _isOnBase && _cupExitedBaseSincePickup)
        {
            _lastDockReason = "FIRED auto-on-base";
            Debug.Log("[Cup] DOCK (AUTO ON BASE)");
            DockToJoystick();
            return;
        }

        // Path B: stillness dwell — cup transform velocity, not joystick anchor.
        // While held, cup follows handGripPoint; this tracks the held cup's actual motion.
        if (dockAllowedByTime && _isOnBase && _cupExitedBaseSincePickup && _cupSpeed < stillSpeed)
            _stillTimer += Time.deltaTime;
        else
            _stillTimer = 0f;

        if (_stillTimer >= stillDwellTime)
        {
            _lastDockReason = $"FIRED still cupSpeed={_cupSpeed:F3} dwell={_stillTimer:F3}";
            Debug.Log($"[Cup] DOCK (STILL) cupSpeed={_cupSpeed:F3} dwell={_stillTimer:F3}");
            DockToJoystick();
            return;
        }

        // Diagnostic: why is dock not firing?
        if (!dockAllowedByTime) _lastDockReason = $"too-soon ({HeldDuration:F2}s/{minHeldTimeBeforeDock:F2}s)";
        else if (!_cupExitedBaseSincePickup) _lastDockReason = "must lift cup off base first";
        else if (!_isOnBase) _lastDockReason = "not on base";
        else if (_cupSpeed >= stillSpeed) _lastDockReason = $"moving cupSpeed={_cupSpeed:F3}>{stillSpeed:F3}";
        else _lastDockReason = $"dwell {_stillTimer:F2}/{stillDwellTime:F2}s";
    }

    // ─────────── State transitions (public API) ───────────

    public void SnapToHand()
    {
        if (handGripPoint == null)
        {
            Debug.LogWarning("[Cup] SnapToHand called but handGripPoint is null.");
            return;
        }

        _heldByHand = true;
        _heldSinceTime = Time.time;
        _isOnBase = false;
        _pickupNearBase1 = IsNearBase(base1, baseRadius);
        _pickupNearBase2 = IsNearBase(base2, baseRadius);
        _cupPosAtPickup = transform.position;
        // If pickup did not happen near any base (rare — joystickAnchor not at a base),
        // arm immediately so the cup can dock on the first base it reaches.
        _cupExitedBaseSincePickup = !(_pickupNearBase1 || _pickupNearBase2);
        _releaseLock = false;
        _stillTimer = 0f;
        _dockCorrectionActive = false;

        OnCupPickedUp?.Invoke();
        ParentTo(handGripPoint);

        Debug.Log("[Cup] >>> SNAP -> HAND <<<");
    }

    public void DockToJoystick()
    {
        if (joystickAnchor == null)
        {
            Debug.LogWarning("[Cup] DockToJoystick called but joystickAnchor is null.");
            return;
        }

        _heldByHand = false;
        _stillTimer = 0f;
        _prevAnchorPos = joystickAnchor.position;
        _anchorSpeed = 0f;
        _prevCupPos = transform.position;
        _cupSpeed = 0f;
        _releaseLock = true;
        _armedForPickup = false;  // disarm: hand must exit + re-enter for next pickup
        _releaseUnlockTime = Time.time + releaseCooldown;

        int which = WhichBase();
        OnCupDocked?.Invoke(which);

        // Cup stays at scene root — Update mirrors anchor pose.
        transform.SetParent(null, worldPositionStays: true);
        if (!_dockPoseCaptured) CaptureDockPoseFromWorld();

        if (dockCorrectionSpeed > 0f && _dockPoseCaptured)
        {
            _dockCorrectionActive = true;
            SnapshotDockRefs();
            Debug.Log($"[Cup] >>> DOCK (SETTLE, world-pose) <<< base={which}");
        }
        else
        {
            ApplyDockWorldPose();
            SnapshotDockRefs();
            Debug.Log($"[Cup] >>> DOCK (SNAP, world-pose) <<< base={which}");
        }
    }

    // ─────────── Helpers ───────────

    private void UpdateAnchorVelocity(float dt)
    {
        if (joystickAnchor != null)
        {
            _anchorSpeed = (joystickAnchor.position - _prevAnchorPos).magnitude / dt;
            _prevAnchorPos = joystickAnchor.position;
        }
        else
        {
            _anchorSpeed = 0f;
        }

        _cupSpeed = (transform.position - _prevCupPos).magnitude / dt;
        _prevCupPos = transform.position;
    }

    private void UpdateReleaseLock()
    {
        if (_releaseLock && Time.time >= _releaseUnlockTime)
        {
            _releaseLock = false;
            SnapshotDockRefs();
        }
    }

    private void RefreshDockRefsIfHandFar()
    {
        bool handClose = _handInsideGrabVolume || EffectorDistanceToCup() <= handNearDistance;
        if (!handClose) SnapshotDockRefs();
    }

    // Mirrors joystickAnchor's world pose to the cup (cup is not parented).
    // Freezes the cup at last pose if the anchor's hierarchy is inactive (XR tracking loss).
    private void UpdateDockedWorldPose(float dt)
    {
        if (joystickAnchor == null || !_dockPoseCaptured) return;

        bool anchorActive = joystickAnchor.gameObject.activeInHierarchy;
        bool controllerActive = controllerActiveProbe == null
                              || controllerActiveProbe.gameObject.activeInHierarchy;
        if (!anchorActive || !controllerActive)
        {
            _anchorWasActive = false;
            return; // freeze at last known world pose (no flicker / no drift during hand-tracking)
        }
        // Tracking just resumed → smooth catch-up instead of snap
        if (!_anchorWasActive)
        {
            _dockCorrectionActive = true;
            _anchorWasActive = true;
        }

        Vector3 targetPos = joystickAnchor.TransformPoint(_dockLocalPos);
        Quaternion targetRot = joystickAnchor.rotation * _dockLocalRot;

        if (_dockCorrectionActive && dockCorrectionSpeed > 0f)
        {
            float alpha = 1f - Mathf.Exp(-dockCorrectionSpeed * dt);
            transform.position = Vector3.Lerp(transform.position, targetPos, alpha);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, alpha);

            if (Vector3.Distance(transform.position, targetPos) <= dockCorrectionDeadZone &&
                Quaternion.Angle(transform.rotation, targetRot) <= 0.1f)
            {
                transform.position = targetPos;
                transform.rotation = targetRot;
                _dockCorrectionActive = false;
            }
        }
        else
        {
            transform.position = targetPos;
            transform.rotation = targetRot;
        }
    }

    private float EffectorDistanceToCup()
    {
        return handGripPoint != null
            ? Vector3.Distance(handGripPoint.position, transform.position)
            : float.MaxValue;
    }

    // Returns the relevant tip-to-cup distance (m) for the pickup gate, or -1 if no usable
    // collider/tip was resolved. Collider.ClosestPoint returns the input point unchanged when
    // it's inside the collider, so points inside report distance 0 — exactly what we want for
    // "contact". Aggregation depends on pickupRequireAllActiveTips:
    //   false → MIN distance among active tips (any-tip contact, legacy)
    //   true  → MAX distance among active tips (all-tip contact, e.g. pollice+indice)
    // closestTip diagnostic always tracks the single nearest tip.
    private float MinEffectorTipDistanceToCup()
    {
        if (_resolvedGrabCollider == null)
        {
            _resolvedGrabCollider = grabCollider != null ? grabCollider : GetComponent<Collider>();
            if (_resolvedGrabCollider == null) { _lastTipDist = -1f; _lastClosestTip = null; return -1f; }
        }
        float minD = float.MaxValue;
        float maxD = -1f;
        Transform closestTip = null;
        int activeCount = 0;
        for (int i = 0; i < effectorTips.Length; i++)
        {
            Transform tip = effectorTips[i];
            if (tip == null || !tip.gameObject.activeInHierarchy) continue;
            activeCount++;
            Vector3 p = tip.position;
            Vector3 closest = _resolvedGrabCollider.ClosestPoint(p);
            float d = (closest - p).magnitude;
            if (d < minD) { minD = d; closestTip = tip; }
            if (d > maxD) maxD = d;
        }
        _lastClosestTip = closestTip;
        if (activeCount == 0) { _lastTipDist = -1f; return -1f; }
        _lastTipDist = pickupRequireAllActiveTips ? maxD : minD;
        return _lastTipDist;
    }

    private void ParentTo(Transform newParent)
    {
        if (newParent == null) return;
        transform.SetParent(newParent, worldPositionStays: true);
    }

    private void SnapshotDockRefs()
    {
        _dockRefCupY = transform.position.y;
        if (joystickAnchor != null)
        {
            _dockRefAnchorPos = joystickAnchor.position;
            _dockRefAnchorRot = joystickAnchor.rotation;
        }
    }

    // Captures dock pose when the cup is still parented to joystickAnchor
    // (editor-time setup). Local transform is already relative to the anchor.
    private void CaptureDockPoseIfNeeded()
    {
        if (_dockPoseCaptured) return;
        if (joystickAnchor == null) return;
        if (transform.parent != joystickAnchor) return;

        _dockLocalPos = transform.localPosition;
        _dockLocalRot = transform.localRotation;
        _dockLocalScale = transform.localScale;
        _dockPoseCaptured = true;
    }

    // Fallback capture when the cup is not parented to joystickAnchor:
    // compute cup pose relative to anchor via inverse world transform.
    private void CaptureDockPoseFromWorld()
    {
        if (joystickAnchor == null) return;
        _dockLocalPos = joystickAnchor.InverseTransformPoint(transform.position);
        _dockLocalRot = Quaternion.Inverse(joystickAnchor.rotation) * transform.rotation;
        _dockLocalScale = transform.localScale;
        _dockPoseCaptured = true;
    }

    // Applies captured dock offset to the cup's world pose (no parenting).
    // Scale is intentionally NOT touched — SetParent(null, worldPositionStays:true)
    // already preserves world scale; setting localScale to the captured value
    // (which was under the anchor's lossyScale) would break the size at root.
    private void ApplyDockWorldPose()
    {
        if (joystickAnchor == null || !_dockPoseCaptured) return;
        transform.position = joystickAnchor.TransformPoint(_dockLocalPos);
        transform.rotation = joystickAnchor.rotation * _dockLocalRot;
    }

    private bool IsOnAnyBase(float radius)
    {
        return IsNearBase(base1, radius) || IsNearBase(base2, radius);
    }

    private bool IsNearBase(Transform baseT, float radius)
    {
        if (baseT == null) return false;
        Vector3 cup = transform.position;
        Vector3 b = baseT.position;
        float dx = cup.x - b.x;
        float dz = cup.z - b.z;
        float xz2 = dx * dx + dz * dz;
        float dy = Mathf.Abs(cup.y - b.y);
        return xz2 <= radius * radius && dy <= baseYTolerance;
    }

    private int WhichBase()
    {
        bool on1 = IsNearBase(base1, baseRadius);
        bool on2 = IsNearBase(base2, baseRadius);
        if (on1 && !on2) return 1;
        if (on2 && !on1) return 2;
        if (on1 && on2) return 3;
        return 0;
    }

    // ─────────── Editor gizmos ───────────
    // Visualize the base zones (entry radius, exit radius, Y tolerance) in the
    // Scene view when the CupSwitchDriver2Bases GameObject is selected. Lets
    // you tune baseRadius / baseExitRadius / baseYTolerance with visual feedback.
    private void OnDrawGizmosSelected()
    {
        DrawBaseGizmos(base1, "base1");
        DrawBaseGizmos(base2, "base2");
    }

    private void DrawBaseGizmos(Transform baseT, string label)
    {
        if (baseT == null) return;
        Vector3 c = baseT.position;
        // Entry radius — green
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.9f);
        DrawCircleXZ(c, baseRadius);
        // Exit radius (hysteresis) — yellow
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.7f);
        DrawCircleXZ(c, Mathf.Max(baseExitRadius, baseRadius));
        // Y tolerance band — cyan vertical line through base center
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.7f);
        Vector3 yTop = c + Vector3.up * baseYTolerance;
        Vector3 yBot = c - Vector3.up * baseYTolerance;
        Gizmos.DrawLine(yTop, yBot);
        // Caps on the Y band
        DrawCircleXZ(yTop, baseRadius * 0.15f);
        DrawCircleXZ(yBot, baseRadius * 0.15f);
    }

    private static void DrawCircleXZ(Vector3 center, float radius, int segments = 64)
    {
        if (radius <= 0f) return;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * (2f * Mathf.PI / segments);
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
