using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioObjectCollision : MonoBehaviour
{
    FMOD.Studio.EventInstance instance;

    [FMODUnity.EventRef]
    public string fmodEvent = null;
    public float parameterValue;
    private bool AllowFadeout=true;
    [SerializeField]
    private string[] collisionTag = null;
    [SerializeField]
    private float minCollisionVolume = 0.1f;
    [SerializeField]
    private float maxCollisionVelocity = 5f;
    [SerializeField]
    private bool useParameter;

    [SerializeField]
    private string parameterName;
    // Start is called before the first frame update
    public Rigidbody rb;
    public float velocità_rb;

    private void Update()
    {
        velocità_rb = rb.linearVelocity.magnitude;
    }
    private float CalculateImpactVolume(float speed)
    {
        float volume;
        volume = CubicEaseOut(speed);
        return volume;
    }

    private float CubicEaseOut(float velocity, float startingValue = 0, float changeInValue = 1)
    {
        return changeInValue * ((velocity = velocity / maxCollisionVelocity - 1) * velocity * velocity + 1) + startingValue;
    }

    // Update is called once per frame
    private void  OnTriggerEnter(Collider other)
    {
        foreach (string col in collisionTag)
        {
            if (other.gameObject.tag == col)
            {
                //transform.GetComponent<Rigidbody>().velocity.magnitude
                parameterValue = CalculateImpactVolume(rb.linearVelocity.magnitude);
                if (parameterValue < minCollisionVolume)
                {
                    return;
                }

                instance = FMODUnity.RuntimeManager.CreateInstance(fmodEvent);
                instance.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));

                if (useParameter)
                {
                    instance.setParameterByName(parameterName, parameterValue);
                }

                instance.start();
                instance.release();
            }
        }
       
    }
    private void OnTriggerExit(Collider other)
    {
        instance.stop(AllowFadeout ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);
        instance.release();
        instance.clearHandle();
    }
}
