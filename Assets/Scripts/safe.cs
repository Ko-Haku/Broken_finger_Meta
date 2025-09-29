using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class safe : MonoBehaviour
{
    // Start is called before the first frame update
    public int punti=0;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("safeblu") && gameObject.tag == "blu")
        { gameObject.tag = "safeblu";
            punti += 1;

        }
        if (other.gameObject.CompareTag("safered") && gameObject.tag == "red")
        { gameObject.tag = "safered";
            punti += 1;
        }
    }
}
