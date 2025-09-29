using UnityEngine;

/// Sovrascrive SOLO l'indice in un rig XR Hands.
/// Da mettere sullo stesso GO che ha i bones.
/// Associa le falangi index1/2/3 e passa dentro un driver con openAmount.
[DefaultExecutionOrder(10000)] // esegui dopo XRHandSkeletonDriver
public class XRIndexModifier : MonoBehaviour
{
    [Header("Joint indice (dal rig XR)")]
    public Transform index1;
    public Transform index2;
    public Transform index3;

    [Header("Driver di apertura")]
    public XRPinchOpenDriver driver;

    [Range(0,1)] public float weight = 1f; // 1=solo script, 0=solo tracking

    public float indexAbductOffsetDeg = 0f; // distorsione sperimentale

    public AnimationCurve indexBend1 = AnimationCurve.Linear(0,0, 1,40);
    public AnimationCurve indexBend2 = AnimationCurve.Linear(0,0, 1,50);
    public AnimationCurve indexBend3 = AnimationCurve.Linear(0,0, 1,25);

    Quaternion i1_base, i2_base, i3_base;

    void Start()
    {
        if (index1) i1_base = index1.localRotation;
        if (index2) i2_base = index2.localRotation;
        if (index3) i3_base = index3.localRotation;
    }

    void LateUpdate()
    {
        if (!driver) return;
        float open = driver.openAmount;

        Quaternion i1_off = Quaternion.AngleAxis(indexAbductOffsetDeg, Vector3.up) *
                            Quaternion.AngleAxis(indexBend1.Evaluate(open), Vector3.right);
        Quaternion i2_off = Quaternion.AngleAxis(indexBend2.Evaluate(open), Vector3.right);
        Quaternion i3_off = Quaternion.AngleAxis(indexBend3.Evaluate(open), Vector3.right);

        if (index1) index1.localRotation = Quaternion.Slerp(index1.localRotation, i1_base * i1_off, weight);
        if (index2) index2.localRotation = Quaternion.Slerp(index2.localRotation, i2_base * i2_off, weight);
        if (index3) index3.localRotation = Quaternion.Slerp(index3.localRotation, i3_base * i3_off, weight);
    }
}