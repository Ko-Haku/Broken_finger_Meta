using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.EventSystems;

public class toccato : MonoBehaviour
{
    public bool tocco1 ;
   
    private GameObject collisore;
    public AudioClip pop;
    public AudioSource ballsource;
    public bool destroyed = false;
   // private TextWriter sWriter_errori = null;
    //private int errori = 0;
    // Start is called before the first frame update
    void Start()
    {
        tocco1 = false;
        ballsource = GetComponent<AudioSource>();
        //string path = Application.dataPath + "/errori.txt";
        
       // sWriter_errori = new StreamWriter(path, append: true);
    }

    // Update is called once per frame
    void Update()
    {
        
           
        
        //if (destroyed)
        //{
        //    ballsource.PlayOneShot(pop);
        //    destroyed = false;
        //    //sWriter_errori.WriteLine("errore_pinza: " + errori +"tempo"+ System.DateTime.Now.ToString("hh.mm.ss.ffffff") + "\n");
        //    //sWriter_errori.Flush();
        //}
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("blu")|| other.CompareTag("red")|| other.CompareTag("giallo"))
        {
            tocco1 = true;
        }
        //if (other.gameObject.CompareTag("boom"))
        //{

        //    Destroy(other.transform.parent.gameObject);
        //    destroyed = true;
        //    errori += 1;
        //}
    }
    void OnTriggerExit(Collider other)
    {
        tocco1 = false;
        
        //print("No longer in contact with " + collisionInfo.transform.name);
    }
    //private void OnApplicationQuit()
    //{

    //    sWriter_errori.Close();
    //}
}
