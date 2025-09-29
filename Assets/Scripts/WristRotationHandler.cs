using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WristRotationHandler : MonoBehaviour
{
    public Transform puntoA;
    public Transform puntoB;
    public float distanza;
    public GameObject realWrist;
    public Transform armTransform; // The transform of the arm to rotate
    public float rotationParameter = 0f; // This should be set between 0 and 1
    public float multiplier=1;
    //public float offset1 = 0.2f;
    public float offset2 = 0f;
    public float multi = 0f;
    // public float multi2 = 0f;

    void Update()
    {
        
        distanza = Vector3.Distance(puntoA.position, puntoB.position)*multiplier;
   
        // Calculate the rotation based on the parameter
        rotationParameter = distanza;
        float rotationAngle = Mathf.Lerp(0, 450, rotationParameter);
        armTransform.localRotation = Quaternion.Euler(0, 0, rotationAngle);
    }
}
