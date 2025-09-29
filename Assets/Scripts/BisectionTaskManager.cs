using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class BisectionTaskManager : MonoBehaviour
{
    public Transform ditoMedioDestra;
    public Transform polsoDestro;
    public Transform indiceSinistro;
    public GameObject colliderLinea;
    public AudioClip suonoRegistrazione;
    public int numeroMisurazioni = 10;

    public List<float> misurazioni = new List<float>();
    private bool misuraRegistrata = false;
    private AudioSource audioSource;

    private bool misurazioneAttiva = false;
    private string idSoggetto;
    private string condizione;
    private string scenaPrecedente;

    void Start()
    {
        idSoggetto = CondizioneStato.idSoggetto;
        condizione = CondizioneStato.condizioneAttuale;
        scenaPrecedente = CondizioneStato.scenaPrecedente;

        if (colliderLinea != null)
        {
            if (!colliderLinea.TryGetComponent<BoxCollider>(out var collider))
            {
                collider = colliderLinea.AddComponent<BoxCollider>();
            }
            collider.isTrigger = true;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = suonoRegistrazione;
        audioSource.playOnAwake = false;

        Invoke("AttivaMisurazione", 10f);
    }

    void AttivaMisurazione()
    {
        misurazioneAttiva = true;
        Debug.Log("Misurazione attivata.");
    }

    void Update()
    {
        if (!misurazioneAttiva || misurazioni.Count >= numeroMisurazioni)
            return;

        AllineaCollider();

        if (colliderLinea.GetComponent<Collider>().bounds.Contains(indiceSinistro.position) && !misuraRegistrata)
        {
            misuraRegistrata = true;
            RegistraMisurazione();
        }

        if (!colliderLinea.GetComponent<Collider>().bounds.Contains(indiceSinistro.position))
        {
            misuraRegistrata = false;
        }
    }

    void AllineaCollider()
    {
        Vector3 origine = ditoMedioDestra.position;
        Vector3 direzione = (polsoDestro.position - ditoMedioDestra.position).normalized;
        float lunghezza = 1f;

        colliderLinea.transform.position = origine + direzione * (lunghezza / 2f);
        colliderLinea.transform.rotation = Quaternion.LookRotation(direzione);
        colliderLinea.transform.localScale = new Vector3(0.2f, 0.2f, lunghezza);
    }

    void RegistraMisurazione()
    {
        Vector3 origine = ditoMedioDestra.position;
        Vector3 direzione = (polsoDestro.position - ditoMedioDestra.position).normalized;
        Vector3 puntoContatto = indiceSinistro.position;

        Vector3 versoPunto = puntoContatto - origine;
        float distanza = Vector3.Dot(versoPunto, direzione);
        distanza = Mathf.Clamp(distanza, 0, 1f);
        float distanzaCm = distanza * 100f;

        misurazioni.Add(distanzaCm);
        Debug.Log($"Misurazione {misurazioni.Count}: {distanzaCm:F1} cm");

        if (audioSource != null && suonoRegistrazione != null)
        {
            audioSource.Play();
        }

        if (misurazioni.Count == numeroMisurazioni)
        {
            SalvaDati();
        }
    }

    void SalvaDati()
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"bisection_hand_log_{idSoggetto}_{condizione}_{scenaPrecedente}_{timestamp}.txt";
        string path = Path.Combine(Application.dataPath, fileName);

        using (StreamWriter writer = new StreamWriter(path))
        {
            writer.WriteLine($"ID Soggetto: {idSoggetto}");
            writer.WriteLine($"Condizione: {condizione}");
            writer.WriteLine($"Provenienza: {scenaPrecedente}");
            writer.WriteLine("Misurazioni bisezione con hand tracking (in cm):");

            foreach (float m in misurazioni)
            {
                writer.WriteLine(m.ToString("F1"));
            }
        }

        Debug.Log($"📁 Dati salvati in: {path}");
    }

    /*void OnDrawGizmos()
    {
        if (ditoMedioDestra != null && polsoDestro != null)
        {
            Gizmos.color = Color.green;

            Vector3 origine = ditoMedioDestra.position;
            Vector3 direzione = (polsoDestro.position - ditoMedioDestra.position).normalized;
            float lunghezza = 0.6f;

            Vector3 puntoInizio = origine;
            Vector3 puntoFine = origine + direzione * lunghezza;

            Gizmos.DrawLine(puntoInizio, puntoFine);
            //Gizmos.DrawWireCube(puntoInizio + direzione * (lunghezza / 2f), new Vector3(0.1f, 0.1f, lunghezza));
        }
    }*/
}
