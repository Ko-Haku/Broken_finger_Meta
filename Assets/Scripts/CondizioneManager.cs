using UnityEngine;
using UnityEngine.SceneManagement;

public class CondizioneManager : MonoBehaviour
{
    public GameObject toolLungoGO;
    public GameObject toolCortoGO;

    void Start()
    {
        ApplicaCondizione();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            SwitchCondizione();
        }
        
       
    }

   

    void SwitchCondizione()
    {
        if (CondizioneStato.condizioneAttuale == "ToolLungo")
        {
            CondizioneStato.condizioneAttuale = "ToolCorto";
        }
        else
        {
            CondizioneStato.condizioneAttuale = "ToolLungo";
        }

        ApplicaCondizione();
    }

    void ApplicaCondizione()
    {
        bool lungo = CondizioneStato.condizioneAttuale == "ToolLungo";
        if (toolLungoGO) toolLungoGO.SetActive(lungo);
        if (toolCortoGO) toolCortoGO.SetActive(!lungo);

        Debug.Log("Condizione attuale: " + CondizioneStato.condizioneAttuale);
    }
}