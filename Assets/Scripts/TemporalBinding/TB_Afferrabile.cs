using System.Collections.Generic;
using UnityEngine;

// Oggetto sul tavolo che si può prendere con la mano, la pinza o il robot (vedi TB_Presa)
// e spingere con le dita (TB_DitaFisiche). Se cade dal tavolo o si preme R torna al suo posto.
[RequireComponent(typeof(Rigidbody))]
public class TB_Afferrabile : MonoBehaviour
{
    public KeyCode tastoReset = KeyCode.R;
    [Tooltip("Sotto questa quota rispetto alla posizione iniziale l'oggetto torna al suo posto (m)")]
    public float cadutaMax = 0.4f;

    [Header("Spinta con le dita")]
    [Tooltip("La mano reale non incontra resistenza: la velocità data da un tocco viene limitata (m/s)")]
    public float velocitaMaxSpinta = 0.8f;
    [Tooltip("rad/s")]
    public float rotazioneMaxSpinta = 12f;

    public Rigidbody Corpo { get; private set; }
    public Collider Forma { get; private set; }
    public TB_Presa TenutoDa { get; set; }

    Vector3 posIniziale;
    Quaternion rotIniziale;
    bool toccato;
    readonly List<Collider> ignorati = new List<Collider>();
    float fineIgnora = -1f;

    void Awake()
    {
        Corpo = GetComponent<Rigidbody>();
        Forma = GetComponent<Collider>();
        posIniziale = transform.position;
        rotIniziale = transform.rotation;
    }

    void Update()
    {
        if (Input.GetKeyDown(tastoReset) || transform.position.y < posIniziale.y - cadutaMax) Riposiziona();
        if (fineIgnora > 0f && !TenutoDa && Time.time > fineIgnora) Ignora(null, false);
    }

    void OnCollisionStay(Collision c) { if (TB_DitaFisiche.EDito(c.rigidbody)) toccato = true; }

    void FixedUpdate()
    {
        if (!toccato || Corpo.isKinematic) { toccato = false; return; }
        toccato = false;
        Corpo.linearVelocity = Vector3.ClampMagnitude(Corpo.linearVelocity, velocitaMaxSpinta);
        Corpo.angularVelocity = Vector3.ClampMagnitude(Corpo.angularVelocity, rotazioneMaxSpinta);
    }

    // Mentre l'oggetto è tenuto (e per un attimo dopo il rilascio) le dita della mano che lo tiene
    // non lo urtano: altrimenti, riaprendo la mano, le sfere dentro l'oggetto lo sparerebbero via.
    public void Ignora(List<Collider> dita, bool on, float durata = 0f)
    {
        if (on)
        {
            Ignora(null, false);
            ignorati.AddRange(dita);
            foreach (var c in ignorati) if (c) Physics.IgnoreCollision(c, Forma, true);
            fineIgnora = -1f;
        }
        else if (durata > 0f) fineIgnora = Time.time + durata;
        else
        {
            foreach (var c in ignorati) if (c) Physics.IgnoreCollision(c, Forma, false);
            ignorati.Clear();
            fineIgnora = -1f;
        }
    }

    public void Riposiziona()
    {
        if (TenutoDa) TenutoDa.Rilascia();
        Ignora(null, false);
        Corpo.isKinematic = false;
        Corpo.linearVelocity = Vector3.zero;
        Corpo.angularVelocity = Vector3.zero;
        Corpo.position = posIniziale;
        Corpo.rotation = rotIniziale;
        transform.SetPositionAndRotation(posIniziale, rotIniziale);
    }

    public float Distanza(Vector3 p) => Forma ? Vector3.Distance(Forma.ClosestPoint(p), p) : Vector3.Distance(transform.position, p);
}
