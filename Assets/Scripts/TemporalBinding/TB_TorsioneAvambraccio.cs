using UnityEngine;

// Pronosupinazione distribuita lungo l'avambraccio (equivalente dei "twist/wrist relaxer").
// L'osso di torsione (creato da costruisci_avatar_tb.py, pesi crescenti dal gomito al polso)
// ruota attorno all'asse dell'avambraccio di quanto la mano è ruotata rispetto all'avambraccio.
// Gira dopo VRIK e dopo TB_ManoRealistica (che fissa la rotazione finale della mano).
[DefaultExecutionOrder(32100)]
public class TB_TorsioneAvambraccio : MonoBehaviour
{
    [System.Serializable]
    public class Lato
    {
        public Transform avambraccio;
        public Transform torsione;
        public Transform mano;
        [Range(0, 1)] public float peso = 1f;
        [Tooltip("Quota della torsione data anche all'avambraccio intero (zona del gomito); la mano resta compensata")]
        [Range(0, 0.6f)] public float pesoGomito = 0.3f;

        internal Quaternion manoRelRiposo, torsioneRiposo;
        internal Vector3 asse;   // asse avambraccio->mano nello spazio dell'avambraccio
    }

    public Lato[] lati;

    void Awake()
    {
        foreach (var l in lati)
        {
            if (!l.avambraccio || !l.torsione || !l.mano) continue;
            l.manoRelRiposo = Quaternion.Inverse(l.avambraccio.rotation) * l.mano.rotation;
            l.torsioneRiposo = l.torsione.localRotation;
            l.asse = l.avambraccio.InverseTransformDirection(l.mano.position - l.avambraccio.position).normalized;
        }
    }

    void LateUpdate()
    {
        foreach (var l in lati)
        {
            if (!l.avambraccio || !l.torsione || !l.mano) continue;
            // rotazione della mano rispetto alla posa di riposo, nello spazio dell'avambraccio
            Quaternion rel = Quaternion.Inverse(l.avambraccio.rotation) * l.mano.rotation;
            Quaternion delta = rel * Quaternion.Inverse(l.manoRelRiposo);
            float ang = AngoloTwist(delta, l.asse) * l.peso;

            // una quota della torsione all'avambraccio intero (relax vicino al gomito),
            // poi si ripristina la rotazione mondo della mano (dita incluse, sono figlie)
            float angGomito = ang * l.pesoGomito;
            if (Mathf.Abs(angGomito) > 0.01f)
            {
                Quaternion manoMondo = l.mano.rotation;
                l.avambraccio.rotation = l.avambraccio.rotation * Quaternion.AngleAxis(angGomito, l.asse);
                l.mano.rotation = manoMondo;
            }
            // il resto della torsione all'osso di torsione (peso crescente verso il polso)
            l.torsione.localRotation = Quaternion.AngleAxis(ang - angGomito, l.asse) * l.torsioneRiposo;
        }
    }

    // Componente di twist (swing-twist) attorno ad 'asse', in gradi con segno
    static float AngoloTwist(Quaternion q, Vector3 asse)
    {
        Vector3 v = new Vector3(q.x, q.y, q.z);
        Vector3 p = Vector3.Project(v, asse);
        var twist = new Quaternion(p.x, p.y, p.z, q.w);
        float n = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
        if (n < 1e-6f) return 0f;
        float w = Mathf.Clamp(twist.w / n, -1f, 1f);
        float ang = 2f * Mathf.Acos(w) * Mathf.Rad2Deg;
        if (Vector3.Dot(p, asse) < 0) ang = -ang;
        if (ang > 180f) ang -= 360f; else if (ang < -180f) ang += 360f;
        return ang;
    }
}
