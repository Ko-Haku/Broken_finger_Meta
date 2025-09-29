using UnityEngine;

public class Contenitore : MonoBehaviour
{
    public string coloreContenitore;
    public ScoreManager scoreManager;

    private Cubetto cubettoDentro;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Grabbable"))
        {
            Cubetto cubo = other.GetComponent<Cubetto>();
            if (cubo != null)
            {
                cubettoDentro = cubo;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Cubetto cubo = other.GetComponent<Cubetto>();
        if (cubo != null && cubo == cubettoDentro)
        {
            cubettoDentro = null;
        }
    }

    public void ControllaRilascio(Cubetto cubo)
    {
        if (cubo != null && cubo == cubettoDentro)
        {
            bool corretto = cubo.colore == coloreContenitore;
            FindObjectOfType<ExperimentManagerCubetti>().LogCubetto(cubo.colore, coloreContenitore, corretto);

            if (corretto)
            {
                // Destroy(cubo.gameObject); // opzionale
            }
        }
    }
}