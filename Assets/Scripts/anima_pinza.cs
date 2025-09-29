using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class anima_pinza : MonoBehaviour
{
    public Animator pinzanim;
    public float distanza;
    
    public Transform dito1;
    public Transform dito2;
    public float multiplier=2;
    void Start()
    {
        pinzanim = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
       distanza = (Vector3.Distance(dito1.position, dito2.position) * 10)-0.15f;

        pinzanim.SetFloat("apertura", distanza*multiplier);

    }
    
}
