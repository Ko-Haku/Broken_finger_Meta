using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class gripper_cut : MonoBehaviour
{
    public Transform puntoA;
    public Transform puntoB;

    public Transform pinz1;
    public Transform pinz2;
    public float x;
    public float y;
    public float z;
    public float w;
    public float x2;
    public float distanza;
    public float dist;
    public float incremento;
    public float velocità;


    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        dist = Vector3.Distance(puntoA.position, puntoB.position);
        //Debug.Log(dist);
        distanza = dist * 100f;
        distanza -= 1.6f;

        pinz2.localRotation = new Quaternion(x, y, z, w) * pinz1.localRotation * new Quaternion(x, y, z, w);
        incremento = (distanza / velocità);
        pinz1.localRotation = Quaternion.Euler(0, 0, x2 * distanza*incremento);
    }
}