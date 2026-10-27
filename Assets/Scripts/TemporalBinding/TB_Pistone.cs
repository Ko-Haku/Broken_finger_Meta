using UnityEngine;

// Agente meccanico della condizione Esterno: l'asta scende e tocca il tasto esattamente
// all'istante programmato del primo evento, poi resta giù finché il manager la ritira.
// La discesa dura quanto una chiusura del pinch, così l'evento è preceduto da un movimento
// visibile come nelle condizioni con la mano o il tool.
public class TB_Pistone : MonoBehaviour
{
    public Transform asta;                   // origine in cima, scende lungo -Y locale del genitore
    [Tooltip("Metri: dalla posizione di riposo al contatto con il tasto")]
    public float corsaContatto = 0.038f;
    [Tooltip("Metri in più dopo il contatto (il tasto si abbassa con l'asta)")]
    public float corsaExtra = 0.006f;
    [Tooltip("Secondi: durata della discesa fino al contatto")]
    public float durataDiscesa = 0.25f;
    public float durataRitorno = 0.3f;

    Vector3 riposo;
    double tContatto = -1, tRitiro = -1;

    public bool InContatto { get; private set; }

    void Awake() { if (asta) riposo = asta.localPosition; Ritira(true); }

    // Programma la discesa in modo che il contatto cada all'istante assoluto tContatto (realtimeSinceStartup)
    public void Programma(double contatto) { tContatto = contatto; tRitiro = -1; InContatto = false; }

    public void Ritira(bool subito = false)
    {
        tContatto = -1; InContatto = false;
        tRitiro = subito ? -1 : Time.realtimeSinceStartupAsDouble;
        if (subito && asta) asta.localPosition = riposo;
    }

    void Update()
    {
        if (!asta) return;
        double now = Time.realtimeSinceStartupAsDouble;
        float scesa = 0f;
        if (tContatto > 0)
        {
            double inizio = tContatto - durataDiscesa;
            if (now < inizio) scesa = 0f;
            else if (now < tContatto)
            {
                float u = (float)((now - inizio) / durataDiscesa);
                scesa = corsaContatto * u * u;   // accelera come un solenoide
            }
            else { scesa = corsaContatto + corsaExtra; InContatto = true; }
        }
        else if (tRitiro > 0)
        {
            float u = Mathf.Clamp01((float)((now - tRitiro) / durataRitorno));
            scesa = (corsaContatto + corsaExtra) * (1f - Mathf.SmoothStep(0f, 1f, u));
            if (u >= 1f) tRitiro = -1;
        }
        asta.localPosition = riposo - asta.parent.InverseTransformVector(Vector3.up) * scesa;
    }
}
