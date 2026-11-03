using UnityEngine;

public class GripperBlendController : MonoBehaviour
{
    public Transform puntoA;
    public Transform puntoB;

    public Animator animator;
    public string openParam = "open";

    [Header("Distance -> Open (cm)")]
    public float neutralDistanceCm = 3f; // 0
    public float openDistanceCm = 7f;    // 1

    [Header("Clamp range (what part of the animation is reachable)")]
    [Tooltip("When near the cup, the pinza cannot close below this value (prevents penetrating the cup).")]
    [Range(0f, 1f)]
    public float minOpen = 0.40f;
    [Tooltip("When far from the cup, the pinza can close freely down to this value.")]
    [Range(0f, 1f)]
    public float freeMinOpen = 0.00f;
    [Tooltip("Maximum aperture (normally 1 = fully open).")]
    [Range(0f, 1f)]
    public float maxOpen = 1.00f;
    public bool invert01 = false;

    [Header("Proximity (drives dynamic clamp)")]
    [Tooltip("When assigned, the clamp follows HandInsideGrabVolume: minOpen when inside, freeMinOpen when outside.")]
    public CupSwitchDriver2Bases cupDriver;
    [Tooltip("Seconds to blend between free and clamped minOpen (prevents snap). Used when releasing.")]
    public float clampBlendTime = 0.18f;
    [Tooltip("Seconds to blend when the clamp needs to OPEN (tip pushing into cup). " +
             "Keep small for snappy anti-penetration response. Asymmetric with clampBlendTime avoids oscillation.")]
    public float clampOpenBlendTime = 0.02f;

    [Header("Tip anti-penetration (proportional: closer to cup = more open)")]
    [Tooltip("Tip transforms (es. pinza1_tip, pinza2_tip). Only Transforms needed — no collider/layer required.")]
    public Transform[] tipTransforms;
    [Tooltip("Cup colliders to check against. Usually the cup's trigger BoxCollider.")]
    public Collider[] cupColliders;
    [Tooltip("Radius of the virtual contact sphere at each tip (m). Contact starts when tip-surface distance <= this.")]
    public float tipSphereRadius = 0.015f;
    [Tooltip("Approach margin (m): push ramps from 0 at this distance outside the cup surface to 1 at the surface. " +
             "Larger = earlier/softer anti-penetration.")]
    public float tipApproachMargin = 0.025f;
    [Tooltip("Peak minOpen applied when tip is AT or past the cup surface. Lower = gripper closes more even on contact.")]
    [Range(0f, 1f)]
    public float tipContactMinOpen = 0.70f;

    [Header("Smoothing")]
    public float smoothTime = 0.06f;

    float current;
    float vel;

    float currentMinOpen;
    float minOpenVel;

    float _lastTipPush;
    /// <summary>Last computed tip-contact push (0 = far, 1 = at/inside cup surface). Read by CupSwitchDriver2Bases as a contact gate.</summary>
    public float TipContactPush => _lastTipPush;

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!animator) { enabled = false; return; }

        currentMinOpen = freeMinOpen;
    }

    void Update()
    {
        if (!puntoA || !puntoB) return;

        float distCm = Vector3.Distance(puntoA.position, puntoB.position) * 100f;

        // raw 0..1 (0=chiuso, 1=aperto)
        float t = Mathf.InverseLerp(neutralDistanceCm, openDistanceCm, distCm);
        t = Mathf.Clamp01(t);

        if (invert01) t = 1f - t;

        // Proportional tip-contact push (smooth, no bounce).
        float tipPush = ComputeTipContactPush();
        _lastTipPush = tipPush;

        float baseline = (cupDriver != null && cupDriver.HandInsideGrabVolume) ? minOpen : freeMinOpen;
        float tipTarget = Mathf.Lerp(baseline, tipContactMinOpen, tipPush);
        float targetMinOpen = Mathf.Max(baseline, tipTarget);

        // Asymmetric smoothing: open fast, release slow (prevents oscillation).
        float blendTime = targetMinOpen > currentMinOpen ? clampOpenBlendTime : clampBlendTime;
        currentMinOpen = Mathf.SmoothDamp(currentMinOpen, targetMinOpen, ref minOpenVel, blendTime);

        float lo = Mathf.Min(currentMinOpen, maxOpen);
        float hi = Mathf.Max(currentMinOpen, maxOpen);
        float target = Mathf.Clamp(t, lo, hi);

        // smooth
        current = Mathf.SmoothDamp(current, target, ref vel, smoothTime);

        animator.SetFloat(openParam, current);
    }

    // Returns a normalized push [0,1]:
    //  0 when all tips are farther than (sphereRadius + approachMargin) from every cup collider surface
    //  1 when a tip is at (or inside) a cup surface
    // Linear ramp in the approach band; binary 1 once inside (ClosestPoint returns the input point
    // when inside a collider, so we can't measure internal depth without a real SphereCollider on the tip).
    float ComputeTipContactPush()
    {
        if (tipTransforms == null || cupColliders == null) return 0f;
        if (tipTransforms.Length == 0 || cupColliders.Length == 0) return 0f;

        float maxPush = 0f;
        float triggerDist = tipSphereRadius + tipApproachMargin;

        for (int i = 0; i < tipTransforms.Length; i++)
        {
            Transform tip = tipTransforms[i];
            if (tip == null) continue;
            Vector3 p = tip.position;
            for (int j = 0; j < cupColliders.Length; j++)
            {
                Collider c = cupColliders[j];
                if (c == null || !c.enabled) continue;

                Vector3 closest = c.ClosestPoint(p);
                float d = (closest - p).magnitude;

                float push;
                if (d <= tipSphereRadius)
                    push = 1f;                                          // at/inside surface
                else if (d >= triggerDist)
                    push = 0f;                                          // far enough
                else
                    push = 1f - (d - tipSphereRadius) / tipApproachMargin; // linear ramp

                if (push > maxPush) maxPush = push;
                if (maxPush >= 1f) return 1f;
            }
        }
        return maxPush;
    }
}
