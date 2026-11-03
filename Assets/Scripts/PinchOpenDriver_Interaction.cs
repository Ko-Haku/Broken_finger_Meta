using UnityEngine;
using Oculus.Interaction.Input;

public class PinchOpenDriver_Interaction : MonoBehaviour
{
    public Hand hand;                    // stessa mano del modifier
    public TIThumbIndexModifier modifier;// referenzia il component sopra
    public Animator gripperAnimator;     // opzionale: pinza, parametro "Open"

    [Header("Calibrazione (metri)")]
    public float dMin = 0.02f;           // mano chiusa
    public float dMax = 0.08f;           // mano ben aperta
    [Header("Smoothing")]
    [Range(0,1)] public float smooth = 0.2f;

   public float openAmount;
   private bool _initialized = false;

   [Header("External freeze")]
   [Tooltip("When true, the pinza Animator parameter is NOT updated — it stays at the last written value. " +
            "openAmount and the hand modifier continue to update normally. " +
            "Used by CupSwitchDriver2Bases during HELD to prevent the pinza from clipping into the cup " +
            "if hand tracking has occlusion artifacts (e.g. index hidden behind cup → false 'closing' signal).")]
   public bool freezeAnimatorOutput = false;

    void Update()
    {
        if (hand == null || !hand.IsConnected) return;
        if (!hand.GetJointPose(HandJointId.HandIndexTip, out var pIdx)) return;
        if (!hand.GetJointPose(HandJointId.HandThumbTip, out var pTh)) return;

        float d = Vector3.Distance(pIdx.position, pTh.position);
        float target = Mathf.InverseLerp(dMin, dMax, d);

        if (!_initialized)
        {
            // First valid frame: snap directly to real value (no smoothing lag)
            openAmount = target;
            _initialized = true;
        }
        else
        {
            openAmount = Mathf.Lerp(openAmount, target, 1f - Mathf.Pow(1f - smooth, Time.deltaTime * 90f));
        }

        if (modifier)       modifier.OpenAmount = openAmount;
        if (gripperAnimator && !freezeAnimatorOutput) gripperAnimator.SetFloat("Open", openAmount);
    }
}