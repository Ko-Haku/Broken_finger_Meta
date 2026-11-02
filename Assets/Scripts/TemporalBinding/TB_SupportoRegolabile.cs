using UnityEngine;

// Pulsante su asta: porta il tasto dove il partecipante fa il pinch con il gomito appoggiato.
// Il partecipante tiene la mano in posa comoda (pollice e indice semiaperti, come per stringere
// il pulsante) e lo sperimentatore preme C (o il thumbstick sinistro): la base scorre sul tavolo
// e la testa sale/scende finché il centro del tasto cade sul punto di presa.
// Il pistone dell'Esterno è figlio della testa, quindi segue.
public class TB_SupportoRegolabile : MonoBehaviour
{
    public TB_PinchSensor sensore;
    public Transform testa;                  // piano superiore del piattello, sull'asse della colonna
    public Transform tasto;
    [Tooltip("Altezza minima/massima del piattello sopra la base (metri): limiti del tubo telescopico")]
    public Vector2 limitiAltezza = new Vector2(0.09f, 0.35f);
    [Tooltip("Il punto medio tra i polpastrelli cade circa a metà tra tasto e piattello")]
    public float offsetPresa = 0.001f;
    public KeyCode tastoCalibra = KeyCode.C;
    [Header("Ingombro")]
    [Tooltip("Oggetto da non toccare con la base (la lampadina)")]
    public Transform ostacolo;
    [Tooltip("Distanza orizzontale minima tra il centro della base e quello dell'ostacolo")]
    public float distanzaMinima = 0.14f;

    Quaternion rotIniziale;
    bool rotSalvata;

    void EvitaOstacolo()
    {
        if (!rotSalvata) { rotIniziale = transform.rotation; rotSalvata = true; }
        Quaternion migliore = rotIniziale; float distMigliore = -1f;
        for (int k = 0; k <= 12; k++)                  // 0, ±15, ±30 ... ±180 gradi
        {
            foreach (int segno in new[] { 1, -1 })
            {
                var r = Quaternion.AngleAxis(segno * k * 15f, Vector3.up) * rotIniziale;
                RuotaAttorno(r);
                float d = DistanzaOrizzontale(transform.position, ostacolo.position);
                if (d >= distanzaMinima) { Debug.Log($"[TB] Asta ruotata di {segno * k * 15}° per non toccare {ostacolo.name}"); return; }
                if (d > distMigliore) { distMigliore = d; migliore = r; }
                if (k == 0) break;
            }
        }
        RuotaAttorno(migliore);
    }

    void RuotaAttorno(Quaternion r)
    {
        // porta la rotazione a r tenendo fermo il tasto
        Vector3 primaTasto = tasto.position;
        transform.rotation = r;
        transform.position += primaTasto - tasto.position;
    }

    static float DistanzaOrizzontale(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

    public float Altezza => testa ? Vector3.Dot(testa.position - transform.position, Vector3.up) : 0f;

    void Update()
    {
        if (Input.GetKeyDown(tastoCalibra) || TB_Input.StickGiu) Calibra();
    }

    [ContextMenu("Calibra sulla mano")]
    public void Calibra()
    {
        if (!sensore || !testa || !tasto) return;
        if (!sensore.Tracciata) { Debug.LogWarning("[TB] Calibrazione pulsante: mano non tracciata"); return; }
        Vector3 p = sensore.PuntoPresa;

        // in orizzontale: sposta tutta l'asta così che il tasto sia sotto il punto di presa
        Vector3 delta = p - tasto.position; delta.y = 0f;
        transform.position += delta;

        // la base non deve finire sulla lampadina: si ruota l'asta attorno al tasto (che resta dov'è),
        // preferendo l'angolo più piccolo rispetto all'orientamento di partenza (colonna dietro il tasto)
        if (ostacolo) EvitaOstacolo();

        // in verticale: solo la testa (scorre nel tubo fisso)
        float h = Mathf.Clamp(p.y - offsetPresa - transform.position.y, limitiAltezza.x, limitiAltezza.y);
        testa.position = new Vector3(testa.position.x, transform.position.y + h, testa.position.z);

        Debug.Log($"[TB] Pulsante calibrato: tasto a {tasto.position:F3}, piattello {h * 100f:F1} cm sopra il tavolo" +
                  (Mathf.Approximately(h, limitiAltezza.x) || Mathf.Approximately(h, limitiAltezza.y) ? " (al limite dell'asta)" : ""));
    }
}
