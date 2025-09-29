using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class anima_mano : MonoBehaviour
{
    public Transform puntoA;
    public Transform puntoB;
    Animator animator;
    public float distanza;
    public float dist;
    public float multiplier = 2;

    // Use this for initialization
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        dist = Vector3.Distance(puntoA.position, puntoB.position);
        Debug.Log(dist);
        distanza = dist * 100;
        

        animator.SetFloat("trigger apertura", distanza * multiplier);
        
        
    }

   
}
