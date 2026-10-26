using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

public class GomuGomuNO : MonoBehaviour
{  
    public Transform object1;
    public Transform object2;
    public float maxDistance = 10.0f;
    public float distanceMultiplier = 1.0f;
    private Vector3 initialPosition;
    
    
    // Update is called once per frame
    void Start()
    {
        // Store the initial position of the object
         initialPosition = new Vector3(-0.0058f,0.0126f,-0.0044f);
    }

    void Update()
    {
      
       
        // Calculate the distance between the two objects
        float distance = Vector3.Distance(object1.position, object2.position);

        // Apply a fixed offset or limit the distance
       // distance = Mathf.Clamp(distance, 0, maxDistance);

        // Modify the z position based on the distance
         Vector3 newPosition = initialPosition;
        newPosition.z += distance ;
        transform.localPosition = newPosition;
    }
}
