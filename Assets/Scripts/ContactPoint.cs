using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ContactPoint : MonoBehaviour
{
    [SerializeField] private KatanaCut katana;

    private Vector3 lastPosition;
    private float velocity;
    public bool tagliato = false;
    private void FixedUpdate()
    {
        velocity = Vector3.Distance(transform.position, lastPosition) / Time.fixedDeltaTime;
        lastPosition = transform.position;
    }

    
    private void OnTriggerEnter(Collider other)
    {
        //var 
        //Debug.Log("tagliato");
        if(other.gameObject.CompareTag("cubetto"))
        {
            tagliato = true; 
            var sliceable = other.gameObject.GetComponent<Sliceable>();
            katana.Cut(transform.position, sliceable, velocity);
        }
        
        //if (sliceable)
        //{
        //    
        //}
    }
    //private void OnTriggerStay(Collider other)
    //{
    //    var sliceable = other.gameObject.GetComponent<Sliceable>();
    //    Debug.Log("tagliato");
    //    tagliato = true;
    //    if (sliceable)
    //    {
    //        katana.Cut(transform.position, sliceable, velocity);
    //    }
    //}
}