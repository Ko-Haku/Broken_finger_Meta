using System.Collections.Generic;
using UnityEngine;

public class SferaTarget : MonoBehaviour
{
    public List<string> ditaEntrate = new List<string>();
    public string coloreSfera; // esempio: "rosso", "blu", "giallo"
    private bool esplosa = false;

    [HideInInspector] public string tempoSpawn;
    [HideInInspector] public System.Diagnostics.Stopwatch cronometro;

    void OnTriggerEnter(Collider other)
    {
        if (esplosa) return;

        if (other.CompareTag("pinzetta"))
        {
            if (!ditaEntrate.Contains(other.name))
            {
                ditaEntrate.Add(other.name);
                Debug.Log("Contatto da: " + other.name);

                if (ditaEntrate.Count >= 2)
                {
                    esplosa = true;

                    string tempoFine = System.DateTime.Now.ToString("HH:mm:ss.fff");
                    string durata = cronometro != null ? cronometro.Elapsed.TotalSeconds.ToString("F3") : "0.000";

                    Debug.Log("💥 Sfera esplosa! Colore: " + coloreSfera);

                    ExperimentManager manager = FindObjectOfType<ExperimentManager>();
                    if (manager != null)
                    {
                        manager.SegnaPunto(coloreSfera);
                       
                    }

                    Destroy(gameObject); // "Scoppia" la sfera
                }
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("pinzetta"))
        {
            ditaEntrate.Remove(other.name);
        }
    }
}