using UnityEngine;
using Oculus.Interaction.Input;

// Inserire su un GO accanto a "FromOVRHandDataSource" / "Hand" e referenziarlo nel campo DataModifier del "Hand".
public class TIThumbIndexModifier : DataModifier<HandDataAsset>
{
    [Header("Controllo apertura (0..1)")]
    [Range(0,1)] public float OpenAmount = 0f;     // pilotalo dal tuo driver (pinch distance)
    [Tooltip("1 = solo script su indice/pollice; 0 = solo tracking")]
    [Range(0,1)] public float Weight = 1f;

    [Header("Distorsione sperimentale (solo indice)")]
    [Tooltip("Deviazione laterale innaturale della falange prossimale (gradi)")]
    public float IndexAbductOffsetDeg = 0f;

    // curva di flessione (gradi) in funzione di OpenAmount (puoi editare da Inspector)
    public AnimationCurve IndexBend1 = AnimationCurve.Linear(0,0, 1,40);
    public AnimationCurve IndexBend2 = AnimationCurve.Linear(0,0, 1,50);
    public AnimationCurve IndexBend3 = AnimationCurve.Linear(0,0, 1,25);
    public AnimationCurve ThumbBend1 = AnimationCurve.Linear(0,0, 1,35);
    public AnimationCurve ThumbBend2 = AnimationCurve.Linear(0,0, 1,45);
    public AnimationCurve ThumbBend3 = AnimationCurve.Linear(0,0, 1,20);

    protected override void Apply(HandDataAsset data)
    {
        // Non toccare nulla se il dato non è valido
        if (!data.IsDataValidAndConnected || Weight <= 0f) return;

        // mano sinistra: inverti l’abduzione laterale per coerenza visiva
        float abductSign = data.Config.Handedness == Handedness.Left ? -1f : 1f;

        // --- INDEX ---
        OverrideJoint(data, HandJointId.HandIndex1,
            Quaternion.AngleAxis(abductSign * IndexAbductOffsetDeg, Vector3.up) *
            Quaternion.AngleAxis(IndexBend1.Evaluate(OpenAmount), Vector3.right));

        OverrideJoint(data, HandJointId.HandIndex2,
            Quaternion.AngleAxis(IndexBend2.Evaluate(OpenAmount), Vector3.right));

        OverrideJoint(data, HandJointId.HandIndex3,
            Quaternion.AngleAxis(IndexBend3.Evaluate(OpenAmount), Vector3.right));

        // --- THUMB ---
        OverrideJoint(data, HandJointId.HandThumb1,
            Quaternion.AngleAxis(ThumbBend1.Evaluate(OpenAmount), Vector3.right));

        OverrideJoint(data, HandJointId.HandThumb2,
            Quaternion.AngleAxis(ThumbBend2.Evaluate(OpenAmount), Vector3.right));

        OverrideJoint(data, HandJointId.HandThumb3,
            Quaternion.AngleAxis(ThumbBend3.Evaluate(OpenAmount), Vector3.right));
    }

    private void OverrideJoint(HandDataAsset data, HandJointId id, Quaternion offsetLocal)
    {
        int idx = (int)id;
        // data.Joints[] sono rotazioni **locali** relative allo scheletro → applica offset in spazio locale
        Quaternion trk = data.Joints[idx];
        Quaternion desired = trk * offsetLocal; // “aggiungo” curvatura/abduzione alla posa tracciata
        data.Joints[idx] = Quaternion.Slerp(trk, desired, Weight);
    }
}
