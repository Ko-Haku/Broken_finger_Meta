using UnityEngine;

public class ContenitoreCucina : MonoBehaviour
{
    public string categoria; // "Frutto", "Bicchiere", "Piatto"
    public string nomeContenitore;

    private OggettoCucina oggettoDentro;

    void OnTriggerEnter(Collider other)
    {
        OggettoCucina oggetto = other.GetComponent<OggettoCucina>();
        if (oggetto != null)
        {
            oggettoDentro = oggetto;
        }
    }

    void OnTriggerExit(Collider other)
    {
        OggettoCucina oggetto = other.GetComponent<OggettoCucina>();
        if (oggetto != null && oggetto == oggettoDentro)
        {
            oggettoDentro = null;
        }
    }

    public void ControllaRilascio(OggettoCucina rilasciato)
    {
        if (rilasciato != null && rilasciato == oggettoDentro)
        {
            bool corretto = rilasciato.categoria == categoria;
            FindObjectOfType<ExperimentManagerCucina>().LogOggetto(
                rilasciato.categoria, rilasciato.nomeOggetto, nomeContenitore, corretto
            );
        }
    }
}