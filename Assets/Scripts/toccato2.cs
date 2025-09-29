using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class toccato2 : MonoBehaviour
{
    public bool tocco2 = false;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("red") ||other.CompareTag("blu")||other.CompareTag("giallo") )
        {
            tocco2 = true;
        }
        
    }
    void OnTriggerExit(Collider other)
    {
        tocco2 = false;
        //print("No longer in contact with " + collisionInfo.transform.name);
    }
}
