using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oculus.Interaction.Input;

// Presa di un TB_Afferrabile con l'effettore visibile: mano realistica, robot o artiglio steampunk.
// La chiusura viene dalla distanza pollice-indice della mano tracciata (come TB_ArtiglioPinch);
// il punto di presa è sull'effettore visto (punte di pollice e indice, o le due dita dell'artiglio),
// così si prende quello che si vede tra le dita. Tenuto, l'oggetto segue il polso o il mozzo.
// Gira dopo il retargeting delle mani (32000), la torsione (32100) e il replay (32500).
[DefaultExecutionOrder(32600)]
public class TB_Presa : MonoBehaviour
{
    public TB_ManoRealistica mano;
    [Tooltip("Se compilato, si prende con l'artiglio invece che con le dita della mano")]
    public TB_ArtiglioPinch artiglio;

    [Header("Distanza pollice-indice (m)")]
    public float dChiuso = 0.015f;
    public float dAperto = 0.08f;

    [Header("Soglie (apertura normalizzata 0-1)")]
    public float sogliaPresa = 0.35f;
    public float sogliaRilascio = 0.5f;
    [Tooltip("Distanza massima tra il punto di presa e la superficie dell'oggetto (m)")]
    public float tolleranza = 0.025f;
    [Tooltip("Con le dita fisiche: si prende appena pollice e indice toccano l'oggetto da lati opposti (m dalla superficie)")]
    public float contatto = 0.02f;
    [Tooltip("Dopo una presa per contatto si lascia riaprendo di questo tanto rispetto all'apertura della presa")]
    public float margineRilascio = 0.15f;

    TB_DitaFisiche fisiche;
    float sogliaRilascioAttuale;
    float noContattoFino;

    static readonly List<TB_Afferrabile> oggetti = new List<TB_Afferrabile>();
    public static void Registra(TB_Afferrabile a) { if (!oggetti.Contains(a)) oggetti.Add(a); }

    public TB_Afferrabile Tenuto { get; private set; }
    public float Apertura { get; private set; } = 1f;
    public Vector3 PuntoPresa { get; private set; }

    Hand Sorgente => artiglio ? artiglio.hand : mano ? mano.hand : null;
    Transform Riferimento => artiglio ? artiglio.mozzo : mano ? mano.polso : transform;

    bool eraAperta;
    Quaternion rotRelativa;
    Vector3 posRelativa;
    readonly Vector3[] storico = new Vector3[4];
    readonly float[] tempi = new float[4];
    int nStorico;

    // dita dell'avatar congelate nella posa del contatto mentre l'oggetto è tenuto
    Transform[] dita;
    Quaternion[] rotDitaPresa;
    TB_Afferrabile mira;    // oggetto tra pollice e indice che la mano sta per prendere: non lo spinge

    void Start()
    {
        foreach (var a in FindObjectsByType<TB_Afferrabile>(FindObjectsSortMode.None)) Registra(a);
        fisiche = artiglio ? null : GetComponent<TB_DitaFisiche>();
        if (artiglio) dita = new[] { artiglio.ditoSup, artiglio.ditoInf };
        else if (mano) dita = new[] { mano.pollice, mano.indice, mano.medio, mano.anulare, mano.mignolo }
                             .SelectMany(d => d).Where(t => t).ToArray();
        else dita = new Transform[0];
        rotDitaPresa = new Quaternion[dita.Length];
    }

    void OnDisable() { Rilascia(); Mira(null); }

    void LateUpdate()
    {
        var h = Sorgente;
        if (h == null || !h.IsConnected
            || !h.GetJointPose(HandJointId.HandIndexTip, out var i)
            || !h.GetJointPose(HandJointId.HandThumbTip, out var t))
        {
            Rilascia();
            return;
        }
        Apertura = Mathf.InverseLerp(dChiuso, dAperto, Vector3.Distance(i.position, t.position));
        if (!CalcolaPuntoPresa()) return;

        if (!Tenuto) Mira(fisiche && fisiche.Attiva && Apertura < 0.95f ? TraLeDita() : null);

        if (Tenuto)
        {
            if (Apertura > sogliaRilascioAttuale) Rilascia();
            else
            {
                Segui();
                // le dita reali possono chiudere di più: quelle viste restano aperte attorno all'oggetto
                for (int k = 0; k < dita.Length; k++) dita[k].localRotation = rotDitaPresa[k];
            }
        }
        else if (Apertura < sogliaPresa)
        {
            if (fisiche && fisiche.Attiva && Time.time > noContattoFino) ProvaContatto();
            // si prende solo chiudendo vicino all'oggetto, non entrando con le dita già chiuse
            if (!Tenuto && eraAperta) Prova();
            eraAperta = false;
        }
        else
        {
            // con le dita fisiche l'oggetto verrebbe spinto via prima del pinch: lo si prende al contatto
            if (fisiche && fisiche.Attiva && Apertura < 0.9f && Time.time > noContattoFino) ProvaContatto();
            if (!Tenuto && Apertura > sogliaRilascio) eraAperta = true;
        }
    }

    // Oggetto il cui centro sta tra le punte di pollice e indice: la mano si sta chiudendo per prenderlo.
    TB_Afferrabile TraLeDita()
    {
        Vector3 p = fisiche.PuntaPollice, i = fisiche.PuntaIndice, s = i - p;
        float l2 = s.sqrMagnitude;
        if (l2 < 1e-6f) return null;
        foreach (var a in oggetti)
        {
            if (!a || (a.TenutoDa && a.TenutoDa != this) || !a.isActiveAndEnabled || !a.Forma) continue;
            Vector3 c = a.Forma.bounds.center;
            float u = Vector3.Dot(c - p, s) / l2;
            if (u < 0f || u > 1f) continue;
            float r = Mathf.Max(a.Forma.bounds.extents.x, a.Forma.bounds.extents.y, a.Forma.bounds.extents.z);
            if (Vector3.Distance(c, p + s * u) < r + 0.015f) return a;
        }
        return null;
    }

    void Mira(TB_Afferrabile a)
    {
        if (a == mira) return;
        if (mira && !mira.TenutoDa) mira.Ignora(null, false);
        mira = a;
        if (mira && fisiche) mira.Ignora(fisiche.Colliders, true);
    }

    void ProvaContatto()
    {
        Vector3 p = fisiche.PuntaPollice, i = fisiche.PuntaIndice;
        foreach (var a in oggetti)
        {
            if (!a || a.TenutoDa || !a.isActiveAndEnabled) continue;
            if (a.Distanza(p) > contatto || a.Distanza(i) > contatto) continue;
            Vector3 c = a.Forma ? a.Forma.bounds.center : a.transform.position;
            if (Vector3.Dot(p - c, i - c) >= 0f) continue;      // pollice e indice dallo stesso lato: è una spinta
            Prendi(a);
            return;
        }
    }

    bool CalcolaPuntoPresa()
    {
        if (artiglio)
        {
            PuntoPresa = (Centro(artiglio.ditoSup) + Centro(artiglio.ditoInf)) * 0.5f;
            return true;
        }
        if (mano && mano.PuntaPollice && mano.PuntaIndice)
        {
            PuntoPresa = (mano.PuntaPollice.position + mano.PuntaIndice.position) * 0.5f;
            return true;
        }
        return false;
    }

    static Vector3 Centro(Transform dito)
    {
        var r = dito.GetComponentsInChildren<Renderer>();
        if (r.Length == 0) return dito.position;
        Vector3 c = Vector3.zero; foreach (var x in r) c += x.bounds.center;
        return c / r.Length;
    }

    void Prova()
    {
        TB_Afferrabile migliore = null;
        float dMin = tolleranza;
        foreach (var a in oggetti)
        {
            if (!a || a.TenutoDa || !a.isActiveAndEnabled) continue;
            float d = a.Distanza(PuntoPresa);
            if (d <= dMin) { dMin = d; migliore = a; }
        }
        if (migliore) Prendi(migliore);
    }

    void Prendi(TB_Afferrabile a)
    {
        Tenuto = a;
        Tenuto.TenutoDa = this;
        Tenuto.Corpo.isKinematic = true;
        if (fisiche) Tenuto.Ignora(fisiche.Colliders, true);
        mira = null;
        for (int k = 0; k < dita.Length; k++) rotDitaPresa[k] = dita[k].localRotation;
        sogliaRilascioAttuale = Mathf.Max(sogliaRilascio, Apertura + margineRilascio);
        var rif = Riferimento;
        rotRelativa = Quaternion.Inverse(rif.rotation) * Tenuto.transform.rotation;
        posRelativa = rif.InverseTransformPoint(Tenuto.transform.position);
        nStorico = 0;
    }

    void Segui()
    {
        var rif = Riferimento;
        Tenuto.transform.SetPositionAndRotation(rif.TransformPoint(posRelativa), rif.rotation * rotRelativa);
        Tenuto.Corpo.position = Tenuto.transform.position;
        Tenuto.Corpo.rotation = Tenuto.transform.rotation;

        int k = nStorico % storico.Length;
        storico[k] = Tenuto.transform.position; tempi[k] = Time.time; nStorico++;
    }

    // Lascia l'oggetto con la velocità degli ultimi frame, così si può anche lanciare.
    public void Rilascia()
    {
        if (!Tenuto) return;
        var a = Tenuto;
        Tenuto = null;
        a.TenutoDa = null;
        a.Corpo.isKinematic = false;
        a.Ignora(null, false, 0.4f);
        noContattoFino = Time.time + 0.5f;   // appena lasciato, le punte sono ancora sull'oggetto
        if (nStorico >= 2)
        {
            int n = Mathf.Min(nStorico, storico.Length);
            int ultimo = (nStorico - 1) % storico.Length, primo = (nStorico - n) % storico.Length;
            float dt = tempi[ultimo] - tempi[primo];
            if (dt > 1e-4f) a.Corpo.linearVelocity = (storico[ultimo] - storico[primo]) / dt;
        }
        eraAperta = false;
    }
}
