using System.Collections.Generic;
using UnityEngine;

// Sfere di collisione sulle dita della mano visibile (mano realistica o robot), così toccando
// un oggetto lo si spinge anche senza afferrarlo. Le sfere sono corpi cinematici separati dalla mano,
// mossi con MovePosition: la fisica vede la loro velocità e spinge gli oggetti in modo credibile.
// Le posizioni non dipendono dall'asse delle ossa: punte estrapolate lungo l'ultima falange.
[DefaultExecutionOrder(32550)]
public class TB_DitaFisiche : MonoBehaviour
{
    public TB_ManoRealistica mano;
    [Tooltip("Raggio delle sfere sulle dita (m, mondo)")]
    public float raggioDito = 0.009f;
    [Tooltip("Raggio della sfera sul palmo (m, mondo)")]
    public float raggioPalmo = 0.028f;
    [Tooltip("Quanto la punta sporge oltre l'ultima articolazione, in lunghezze dell'ultima falange")]
    public float estensionePunta = 0.8f;

    static readonly HashSet<Rigidbody> dita = new HashSet<Rigidbody>();
    public static bool EDito(Rigidbody rb) => rb && dita.Contains(rb);

    public readonly List<Collider> Colliders = new List<Collider>();
    readonly List<Rigidbody> corpi = new List<Rigidbody>();
    readonly List<Vector3> bersagli = new List<Vector3>();
    public Vector3 PuntaPollice => bersagli.Count > 3 ? bersagli[3] : transform.position;
    public Vector3 PuntaIndice => bersagli.Count > 7 ? bersagli[7] : transform.position;
    public bool Attiva => pronta && visibile;
    Transform[][] ossa;
    Transform radice;
    bool visibile, pronta;

    void Start()
    {
        if (!mano) mano = GetComponent<TB_ManoRealistica>();
        // fisica allineata al refresh del visore, così le dita non scattano a 50 Hz
        if (TB_Timing.Instance && TB_Timing.Instance.FrequenzaHz > 0)
            Time.fixedDeltaTime = Mathf.Min(Time.fixedDeltaTime, 1f / TB_Timing.Instance.FrequenzaHz);

        ossa = new[] { mano.pollice, mano.indice, mano.medio, mano.anulare, mano.mignolo };
        radice = new GameObject($"TB_DitaFisiche_{transform.root.name}_{name}").transform;
        foreach (var dito in ossa)
            for (int k = 0; k < 4; k++) Crea(raggioDito);   // falange intermedia, articolazione, metà ultima falange, punta
        Crea(raggioPalmo);
        CalcolaBersagli();
        for (int i = 0; i < corpi.Count; i++) corpi[i].transform.position = bersagli[i];
        pronta = true;
        Accendi(true);
    }

    void Crea(float r)
    {
        var go = new GameObject("Sfera");
        go.transform.SetParent(radice, false);
        var s = go.AddComponent<SphereCollider>();
        s.radius = r;
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        Colliders.Add(s); corpi.Add(rb); bersagli.Add(Vector3.zero);
        dita.Add(rb);
    }

    void CalcolaBersagli()
    {
        int i = 0;
        foreach (var d in ossa)
        {
            Vector3 a = d[0].position, b = d[1].position, c = d[2].position;
            Vector3 punta = c + (c - b) * estensionePunta;
            bersagli[i++] = (a + b) * 0.5f;
            bersagli[i++] = c;
            bersagli[i++] = (c + punta) * 0.5f;
            bersagli[i++] = punta;
        }
        // palmo: tra il polso e le nocche di indice, medio e anulare, un po' verso le dita
        Vector3 nocche = (mano.indice[0].position + mano.medio[0].position + mano.anulare[0].position) / 3f;
        bersagli[i] = Vector3.Lerp(mano.polso.position, nocche, 0.6f);
    }

    // Dopo il retargeting (32000) e l'IK: posizioni finali delle ossa visibili.
    void LateUpdate()
    {
        if (!pronta) return;
        bool ora = mano.renderers != null && mano.renderers.Length > 0 && mano.renderers[0] && mano.renderers[0].enabled;
        if (ora != visibile) Accendi(ora);
        if (visibile) CalcolaBersagli();
    }

    void FixedUpdate()
    {
        if (!pronta || !visibile) return;
        for (int i = 0; i < corpi.Count; i++) corpi[i].MovePosition(bersagli[i]);
    }

    // mano non tracciata o avatar nascosto: niente collisioni, e al ritorno le sfere si teletrasportano
    void Accendi(bool on)
    {
        visibile = on;
        if (!radice) return;
        if (on)
        {
            CalcolaBersagli();
            for (int i = 0; i < corpi.Count; i++) { corpi[i].position = bersagli[i]; corpi[i].transform.position = bersagli[i]; }
        }
        radice.gameObject.SetActive(on);
    }

    void OnDisable() { if (pronta) Accendi(false); }
    void OnEnable() { if (pronta) Accendi(false); }   // si riaccende al primo frame visibile

    void OnDestroy()
    {
        foreach (var rb in corpi) dita.Remove(rb);
        if (radice) Destroy(radice.gameObject);
    }
}
