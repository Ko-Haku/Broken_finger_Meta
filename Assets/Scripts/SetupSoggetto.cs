using UnityEngine;

public class SetupSoggetto : MonoBehaviour
{
    [Tooltip("Inserisci l'ID del soggetto, es: S1, P03, etc.")]
    public string idSoggetto = "S1";

    [Tooltip("Tool iniziale da usare")]
    public string condizioneIniziale = "ToolLungo";

    public static SetupSoggetto Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Elimina i duplicati
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // Persiste tra scene

        CondizioneStato.idSoggetto = idSoggetto;
        CondizioneStato.condizioneAttuale = condizioneIniziale;

        Debug.Log($"✅ [SetupSoggetto] ID: {idSoggetto}, Condizione iniziale: {condizioneIniziale}");
    }
}