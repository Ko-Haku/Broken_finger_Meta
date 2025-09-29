using UnityEngine;

public class ParticipantInfo : MonoBehaviour
{
    private static ParticipantInfo instance;

    public static ParticipantInfo Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ParticipantInfo>();
                if (instance == null)
                {
                    GameObject singletonObject = new GameObject();
                    instance = singletonObject.AddComponent<ParticipantInfo>();
                    singletonObject.name = typeof(ParticipantInfo).ToString() + " (Singleton)";
                    DontDestroyOnLoad(singletonObject);
                }
            }
            return instance;
        }
    }

    [Header("Participant Information")]
    public string Codice_Soggetto;
    public string Condizione;
    public string NumeroSoggetto;

    public void SetNomeCognome(string newNome, string newCognome,  string newnumeroSoggetto)
    {
        Codice_Soggetto = newNome;
        Condizione = newCognome;
        NumeroSoggetto = newnumeroSoggetto;
    }
}
