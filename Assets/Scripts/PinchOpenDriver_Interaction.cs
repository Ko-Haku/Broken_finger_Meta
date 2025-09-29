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

    void Update()
    {
        if (hand == null || !hand.IsConnected) return;
        if (!hand.GetJointPose(HandJointId.HandIndexTip, out var pIdx)) return;
        if (!hand.GetJointPose(HandJointId.HandThumbTip, out var pTh)) return;

        float d = Vector3.Distance(pIdx.position, pTh.position);
        float target = Mathf.InverseLerp(dMin, dMax, d);
        openAmount = Mathf.Lerp(openAmount, target, 1f - Mathf.Pow(1f - smooth, Time.deltaTime * 90f));

        if (modifier)       modifier.OpenAmount = openAmount;
        if (gripperAnimator) gripperAnimator.SetFloat("Open", openAmount);
    }
}