using UnityEngine;

public class TweezersController : MonoBehaviour
{
    public Transform tweezerArm1;
    public Transform tweezerArm2;
    public Transform tweezerMid;
    public float a=0.5f;
    public float b=0.5f;

    private void Update()
    {
        // Interpolate the position and rotation between the tweezer arm GameObjects
        Vector3 midPosition = Vector3.Lerp(tweezerArm1.position, tweezerArm2.position, a);
        Quaternion midRotation = Quaternion.Lerp(tweezerArm1.rotation, tweezerArm2.rotation, b);

        // Apply the interpolated position and rotation to the mid part of the tweezers
        tweezerMid.position = midPosition;
        tweezerMid.rotation = midRotation;
    }
}
