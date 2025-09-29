#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.EventSystems;

public class controllerscena1 : MonoBehaviour
{
    
    public GameObject[] condizioni;
    public GameObject[] linee60;
    public GameObject[] linee120;
    public GameObject separatore;
    public GameObject asterisco;
    public GameObject separatore120;
    public GameObject asterisco120;
    private string path;
    public List<int> listarette;
   // private List<int> listarett2e=new List<int> { 0,0,0,0,0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5 };
    int rettarandom;
    private TextWriter sWriter = null;
    

    private int condizione_corrente = -1;
    
    //private int nrTools;
    public bool intprova = false;

    public void cambia_condizione(int num)
    {
        //currentTool = num;
        for (int i = 0; i < condizioni.Length; i++)
        {
            if (i == num)
                condizioni[i].gameObject.SetActive(true);
            else
                condizioni[i].gameObject.SetActive(false);
        }
    }
    // Start is called before the first frame update


    
   
    void Start()
    {
        string path = Application.dataPath + "/Log_esperimento.txt";
        
        sWriter = new StreamWriter(path,append: true);
        sWriter.WriteLine("Login date: " + System.DateTime.Now + "\n");
        sWriter.Flush();

      //  List<int> listarette = new List<int> { 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6, 6 };
    }
    //void Appendfile(string path)
    //{
    //    StreamWriter sWriter;
       
    //   sWriter = new StreamWriter(path, append: true);
    //   sWriter.WriteLine("ciao");
        
    //    sWriter.Close();
        
    //}

    // Update is called once per frame
    void Update()
    {


        if (listarette.Count == 0)
        { listarette = new List<int> {0,0,0,0,0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5};
            Debug.Log("FINE ESPERIMENTO");
        }




        if (Input.GetKeyDown(KeyCode.A))
        {
            if (condizione_corrente == 3) { condizione_corrente = 0; }
            else { condizione_corrente = condizione_corrente + 1; }


            cambia_condizione(condizione_corrente);
           int ccor = condizione_corrente + 1;
            sWriter.WriteLine("condizione:"+ccor.ToString("F2"));
            sWriter.Flush();
            
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        { sWriter.Close(); }

        if (Input.GetKeyDown(KeyCode.T)||Input.GetKey("right"))
        {
            Debug.Log("pressed");
            if (condizione_corrente == 0)
            { StartCoroutine(esperimento60()); }
            else
            { StartCoroutine(esperimento120()); }
            

        }
    }
    IEnumerator esperimento60()
    {
        Debug.Log("sizelista"+listarette.Count);
            asterisco.SetActive(true);
            yield return new WaitForSeconds(0.0250f);
            asterisco.SetActive(false);
            yield return new WaitForSeconds(1f);
        
            rettarandom = Random.Range(0,listarette.Count);
        Debug.Log("rettarandom="+rettarandom);
            linee60[listarette[rettarandom]].SetActive(true);
        Debug.Log(linee60[listarette[rettarandom]]);
       
        
        separatore.SetActive(true);
            yield return new WaitForSeconds(0.0150f);
        linee60[listarette[rettarandom]].SetActive(false);
        separatore.SetActive(false);
        sWriter.WriteLine("retta60:" + listarette[rettarandom]+":"+ linee60[listarette[rettarandom]]);
            sWriter.Flush();
        listarette.RemoveAt(rettarandom);
        Debug.Log("sizelistadopo" + listarette.Count);
        yield return new WaitForSeconds(2.5f);
        
       
       

    }
    IEnumerator esperimento120()
    {

        Debug.Log("sizelista" + listarette.Count);
        asterisco.SetActive(true);
        yield return new WaitForSeconds(0.0250f);
        asterisco.SetActive(false);
        yield return new WaitForSeconds(1f);

        rettarandom = Random.Range(0, listarette.Count);
        Debug.Log("rettarandom=" + rettarandom);
        linee120[listarette[rettarandom]].SetActive(true);
        Debug.Log(linee120[listarette[rettarandom]]);


        separatore.SetActive(true);
        yield return new WaitForSeconds(0.0150f);
        linee120[listarette[rettarandom]].SetActive(false);
        separatore.SetActive(false);
        sWriter.WriteLine("retta120:" + listarette[rettarandom]+":"+ linee120[listarette[rettarandom]]);
        sWriter.Flush();
        listarette.RemoveAt(rettarandom);
        Debug.Log("sizelistadopo" + listarette.Count);
        yield return new WaitForSeconds(2.5f);


    }
    public void retta(BaseEventData eventData)
    {
        if (condizioni[0].activeSelf==true)
        { StartCoroutine(esperimento60()); }
        else
        { StartCoroutine(esperimento120()); }
    }
    private void OnApplicationQuit()
    {
        sWriter.Close();
    }
}
#endif 