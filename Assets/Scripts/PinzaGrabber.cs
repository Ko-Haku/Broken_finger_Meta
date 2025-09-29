using System.Collections.Generic;
using UnityEngine;

public class PinzaGrabber : MonoBehaviour
{
    public Transform grabParent; // punto dove tenere gli oggetti (es: centro tra le dita)
    public string objectTag = "Grabbable";
    private List<string> ditaEntrate = new List<string>();
    private GameObject oggettoPreso;
    private bool haPreso = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("pinzetta") && !ditaEntrate.Contains(other.name))
        {
            ditaEntrate.Add(other.name);
            Debug.Log("Contatto da: " + other.name);

            if (ditaEntrate.Count >= 2 && !haPreso)
            {
                TryGrab();
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (ditaEntrate.Contains(other.name))
        {
            ditaEntrate.Remove(other.name);
            if (haPreso && ditaEntrate.Count < 2)
            {
                Release();
            }
        }
    }
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.02f);
    }
    void TryGrab()
    {
        Collider[] colliders = Physics.OverlapSphere(grabParent.position, 0.02f);
        foreach (var col in colliders)
        {
            if (col.CompareTag(objectTag))
            {
                oggettoPreso = col.gameObject;
                oggettoPreso.transform.SetParent(grabParent);
                oggettoPreso.GetComponent<Rigidbody>().isKinematic = true;
                haPreso = true;
                Debug.Log("Oggetto afferrato: " + oggettoPreso.name);
                break;
            }
        }
    }

    void Release()
    {
        if (oggettoPreso != null)
        {
            oggettoPreso.transform.SetParent(null);
            Rigidbody rb = oggettoPreso.GetComponent<Rigidbody>();
            rb.isKinematic = false;

            // 🔍 Controllo contenitori CUCINA
            OggettoCucina cucina = oggettoPreso.GetComponent<OggettoCucina>();
            if (cucina != null)
            {
                foreach (var contenitore in FindObjectsOfType<ContenitoreCucina>())
                {
                    contenitore.ControllaRilascio(cucina);
                }
            }

            // 🔍 Controllo contenitori CUBETTI
            Cubetto cubetto = oggettoPreso.GetComponent<Cubetto>();
            if (cubetto != null)
            {
                foreach (var contenitore in FindObjectsOfType<Contenitore>())
                {
                    contenitore.ControllaRilascio(cubetto);
                }
            }

            Debug.Log("Oggetto rilasciato: " + oggettoPreso.name);
            oggettoPreso = null;
            haPreso = false;
        }
    }

}