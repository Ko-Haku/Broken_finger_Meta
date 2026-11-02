using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Oculus.Interaction.Input;

// Presa "perfetta" del bicchiere per i video stimoli (mano, tentacolo, gripper): niente fisica, la presa
// è una posa registrata per dimostrazione e il bicchiere si aggancia esattamente lì.
//  - Registrazione (una volta per effettore): bicchiere fermo sul cilindro, avvicini l'effettore come ti viene
//    naturale e chiudi fino al contatto, poi P (o X sul controller sinistro). Si salvano la posa del bicchiere
//    rispetto all'effettore, la distanza pollice-indice reale, l'apertura del tool e (mano) la posa delle dita.
//    Backspace cancella la posa dell'effettore attivo. Le pose stanno in Assets/Scripts/TemporalBinding/PresePose.
//  - Presa: effettore attorno al bicchiere (entro 'raggioPresa' dalla posa salvata) e dita chiuse almeno quanto
//    in registrazione -> il bicchiere scivola in ~0.1 s nella posa salvata; dita/ganasce restano su quella chiusura.
//  - Appoggio: quando il fondo tocca il tavolo lontano dal cilindro il bicchiere si stacca, dritto sul piano,
//    e la mano/il tool torna a seguire le dita. Riaprendo molto le dita lo si lascia dove si trova (appoggiato
//    sotto). R lo rimette sul cilindro.
[DefaultExecutionOrder(32700)]       // dopo retargeting delle dita (32000), torsione (32100), dita fisiche/presa (32550-32600)
public class TB_PresaStimolo : MonoBehaviour
{
    [Serializable]
    public class Effettore
    {
        public string nome;                    // nome dell'avatar (AVATAR_Mano, AVATAR_Tentacolo, ...)
        public GameObject avatar;
        public Transform ancora;               // osso della mano o radice del tool
        public Hand hand;                      // mano tracciata (distanza pollice-indice)
        public TB_AperturaTool tool;           // null per la mano
        public Transform[] dita;               // mano: ossa delle dita da mettere in posa
    }

    [Serializable]
    class Posa
    {
        public Vector3 pos; public Quaternion rot;          // bicchiere nello spazio dell'ancora
        public float pinchCm; public float apertura = -1f;  // distanza reale pollice-indice, apertura del tool
        public Quaternion[] dita;
    }

    public List<Effettore> effettori = new List<Effettore>();
    public Transform cilindro;
    public Transform tavolo;
    [Tooltip("Distanza massima (m) tra il bicchiere e la sua posizione nella posa salvata per agganciarlo")]
    public float raggioPresa = 0.05f;
    [Tooltip("Margine (cm) sulla distanza pollice-indice registrata: si aggancia sotto pinch registrato + margine")]
    public float marginePinchCm = 1.0f;
    [Tooltip("Aprendo le dita di questi cm oltre il pinch registrato il bicchiere si lascia")]
    public float aperturaRilascioCm = 4f;
    public float durataAggancio = 0.12f, durataRilascio = 0.25f;
    public KeyCode tastoRegistra = KeyCode.P, tastoCancella = KeyCode.Backspace, tastoReset = KeyCode.R;

    public static string Cartella => Path.Combine(Application.dataPath, "Scripts", "TemporalBinding", "PresePose");

    readonly Dictionary<string, Posa> pose = new Dictionary<string, Posa>();
    Effettore presa;                 // effettore che tiene il bicchiere
    float tAggancio = -1f, tRilascio = -1f;
    Effettore rilasciato;
    Vector3 posAggancio; Quaternion rotAggancio;
    Vector3 posIniziale; Quaternion rotIniziale;
    Renderer rend;
    Quaternion[] ditaRilascio;

    void Start()
    {
        rend = GetComponentInChildren<Renderer>();
        posIniziale = transform.position; rotIniziale = transform.rotation;
        foreach (var rb in GetComponentsInChildren<Rigidbody>()) { rb.isKinematic = true; rb.useGravity = false; }
        foreach (var e in effettori) Carica(e.nome);
    }

    Effettore Attivo => effettori.FirstOrDefault(e => e.avatar && e.avatar.activeInHierarchy);

    float PinchCm(Effettore e)
    {
        if (e.hand == null || !e.hand.IsConnected) return float.MaxValue;
        if (!e.hand.GetJointPose(HandJointId.HandIndexTip, out var a) || !e.hand.GetJointPose(HandJointId.HandThumbTip, out var b)) return float.MaxValue;
        return Vector3.Distance(a.position, b.position) * 100f;
    }

    void Update()
    {
        var e = Attivo;
        if (Input.GetKeyDown(tastoReset)) Reset_();
        if (e == null) return;
        if (presa == null && (Input.GetKeyDown(tastoRegistra) || TB_Input.XGiu)) Registra(e);
        if (presa == null && Input.GetKeyDown(tastoCancella)) Cancella(e.nome);
    }

    void LateUpdate()
    {
        var e = Attivo;
        // chi teneva il bicchiere non è più attivo (cambio avatar): lo si lascia dove si trova
        if (presa != null && presa != e) Lascia(false);

        if (presa == null && e != null && pose.TryGetValue(e.nome, out var p) && tRilascio < 0f)
        {
            Vector3 bersaglio = e.ancora.TransformPoint(p.pos);
            if (Vector3.Distance(bersaglio, transform.position) < raggioPresa && PinchCm(e) < p.pinchCm + marginePinchCm)
            {
                presa = e; tAggancio = Time.time; posAggancio = transform.position; rotAggancio = transform.rotation;
                Debug.Log($"[TB] Bicchiere preso con {e.nome}");
            }
        }

        if (presa != null)
        {
            var q = pose[presa.nome];
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - tAggancio) / durataAggancio));
            Vector3 pos = presa.ancora.TransformPoint(q.pos); Quaternion rot = presa.ancora.rotation * q.rot;
            transform.SetPositionAndRotation(Vector3.Lerp(posAggancio, pos, k), Quaternion.Slerp(rotAggancio, rot, k));
            ApplicaChiusura(presa, q, k);

            if (k >= 1f && ToccaTavolo()) { Appoggia(); Lascia(true); }
            else if (k >= 1f && PinchCm(presa) > q.pinchCm + aperturaRilascioCm) Lascia(true);
        }
        else if (rilasciato != null && tRilascio >= 0f)
        {
            // la mano/il tool si riapre tornando alla posa tracciata
            float k = Mathf.Clamp01((Time.time - tRilascio) / durataRilascio);
            if (pose.TryGetValue(rilasciato.nome, out var q)) ApplicaChiusura(rilasciato, q, 1f - k);
            if (k >= 1f) { if (rilasciato.tool) rilasciato.tool.aperturaForzata = -1f; rilasciato = null; tRilascio = -1f; }
        }
    }

    void ApplicaChiusura(Effettore e, Posa q, float peso)
    {
        if (e.tool)
        {
            e.tool.aperturaForzata = peso <= 0f ? -1f : Mathf.Lerp(e.tool.Apertura, q.apertura, peso);
        }
        else if (e.dita != null && q.dita != null)
            for (int i = 0; i < e.dita.Length && i < q.dita.Length; i++)
                if (e.dita[i]) e.dita[i].localRotation = Quaternion.Slerp(e.dita[i].localRotation, q.dita[i], peso);
    }

    // fondo del bicchiere sul piano del tavolo, fuori dal cilindro
    bool ToccaTavolo()
    {
        if (!tavolo || !rend) return false;
        float yTavolo = TopTavolo();
        var b = rend.bounds;
        bool sulCilindro = cilindro && Vector2.Distance(new Vector2(b.center.x, b.center.z), new Vector2(cilindro.position.x, cilindro.position.z)) < cilindro.lossyScale.x * 0.5f + b.extents.x;
        return !sulCilindro && b.min.y <= yTavolo + 0.003f;
    }

    float TopTavolo() => tavolo.GetComponentsInChildren<Renderer>().Max(r => r.bounds.max.y);

    // dritto (solo la rotazione attorno alla verticale) e con il fondo esattamente sul piano sotto di lui
    void Appoggia()
    {
        Vector3 avanti = Vector3.ProjectOnPlane(transform.rotation * Quaternion.Inverse(rotIniziale) * Vector3.forward, Vector3.up);
        if (avanti.sqrMagnitude < 1e-4f) avanti = Vector3.forward;
        transform.rotation = Quaternion.LookRotation(avanti.normalized, Vector3.up) * rotIniziale;
        float piano = TopTavolo();
        var b = rend.bounds;
        if (cilindro && Vector2.Distance(new Vector2(b.center.x, b.center.z), new Vector2(cilindro.position.x, cilindro.position.z)) < cilindro.lossyScale.x * 0.5f)
            piano = cilindro.GetComponent<Renderer>().bounds.max.y;
        transform.position += Vector3.up * (piano + 0.0005f - rend.bounds.min.y);
    }

    void Lascia(bool riapri)
    {
        if (presa == null) return;
        Debug.Log($"[TB] Bicchiere lasciato da {presa.nome}");
        if (riapri) { rilasciato = presa; tRilascio = Time.time; }
        else if (presa.tool) presa.tool.aperturaForzata = -1f;
        presa = null;
        Appoggia();
    }

    void Reset_()
    {
        if (presa != null) Lascia(true);
        transform.SetPositionAndRotation(posIniziale, rotIniziale);
    }

    // ------------------------------------------------------------------ registrazione delle pose

    void Registra(Effettore e)
    {
        var p = new Posa
        {
            pos = e.ancora.InverseTransformPoint(transform.position),
            rot = Quaternion.Inverse(e.ancora.rotation) * transform.rotation,
            pinchCm = PinchCm(e),
            apertura = e.tool ? e.tool.Apertura : -1f,
            dita = e.dita != null ? e.dita.Select(d => d ? d.localRotation : Quaternion.identity).ToArray() : null,
        };
        if (p.pinchCm > 100f) { Debug.LogWarning("[TB] Posa di presa non salvata: mano non tracciata"); return; }
        pose[e.nome] = p;
        Directory.CreateDirectory(Cartella);
        File.WriteAllText(Path.Combine(Cartella, e.nome + ".json"), JsonUtility.ToJson(p, true));
        Debug.Log($"[TB] Posa di presa salvata per {e.nome}: pinch {p.pinchCm:F1} cm" + (e.tool ? $", apertura tool {p.apertura:F2}" : $", {p.dita?.Length ?? 0} ossa delle dita"));
    }

    void Carica(string nome)
    {
        string f = Path.Combine(Cartella, nome + ".json");
        if (!File.Exists(f)) return;
        pose[nome] = JsonUtility.FromJson<Posa>(File.ReadAllText(f));
        Debug.Log($"[TB] Posa di presa caricata per {nome}");
    }

    void Cancella(string nome)
    {
        pose.Remove(nome);
        string f = Path.Combine(Cartella, nome + ".json");
        if (File.Exists(f)) File.Delete(f);
        Debug.Log($"[TB] Posa di presa cancellata per {nome}: registrala di nuovo con P");
    }
}
