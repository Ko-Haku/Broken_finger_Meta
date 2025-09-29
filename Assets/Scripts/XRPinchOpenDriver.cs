using UnityEngine;

public class XRPinchOpenDriver : MonoBehaviour
{
    [Header("Joint tip da XR Hands")]
    public Transform indexTip;
    public Transform thumbTip;

    [Header("Output")]
    [Range(0,1)] public float openAmount; // 0=chiuso, 1=aperto

    [Header("Calibrazione distanza (m)")]
    public float dMin = 0.02f;   // mano chiusa
    public float dMax = 0.08f;   // mano aperta

    [Range(0,1)] public float smooth = 0.2f;

    float _open;

    void Update()
    {
        if (!indexTip || !thumbTip) return;

        float d = Vector3.Distance(indexTip.position, thumbTip.position);
        float target = Mathf.InverseLerp(dMin, dMax, d);
        _open = Mathf.Lerp(_open, target, 1f - Mathf.Pow(1f - smooth, Time.deltaTime * 90f));

        openAmount = _open;
    }
}