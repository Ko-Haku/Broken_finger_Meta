using System.Collections.Generic;
using UnityEngine;

// Riproduce sull'avatar le clip registrate con TB_RegistratoreClip (condizione passiva).
// 'fantoccio' è la radice registrata (la spalla destra dell'avatar): durante il replay la clip
// sovrascrive, dopo VRIK, retargeting e torsione, tutte le ossa del braccio e il tool, mentre la mano
// reale resta ferma a riposo. Per questo gira per ultimo (ordine 32500).
// L'istante di chiusura di ogni clip è l'evento "TB_Chiusura" scritto dal registratore
// (la chiusura rilevata dal TB_PinchSensor durante la registrazione); in mancanza, la distanza
// tra due punte (puntaA/puntaB) che scende sotto soglia.
[DefaultExecutionOrder(32500)]
public class TB_Cinematica : MonoBehaviour
{
    public const string EventoChiusura = "TB_Chiusura";

    [Tooltip("Radice registrata: stessa gerarchia dell'oggetto su cui girava TB_RegistratoreClip")]
    public GameObject fantoccio;
    public AnimationClip[] clips;
    [Tooltip("In editor, se 'clips' è vuoto, carica le clip di Registrazioni che iniziano con questo prefisso")]
    public string prefissoClip = "";

    [Header("Solo dita")]
    [Tooltip("Se attivo la clip muove solo le ossa sotto 'mano' (dita, ganasce): braccio e polso restano dal vivo")]
    public bool soloDita = true;
    [Tooltip("Osso della mano destra dell'avatar (mixamorig_RightHand)")]
    public Transform mano;

    [Header("Rilevamento chiusura senza evento (fallback)")]
    public Transform puntaA;
    public Transform puntaB;
    public float sogliaChiusura = 0.015f;   // metri
    public float passoCampionamento = 1f / 240f;

    readonly Dictionary<AnimationClip, float> tChiusuraCache = new Dictionary<AnimationClip, float>();

    AnimationClip clipPreparata;
    AnimationClip clipInCorso;
    double tInizio;
    bool inPosa;

    public double TempoAllaChiusura => clipPreparata != null ? tChiusuraCache[clipPreparata] : 0;
    public bool InRiproduzione => clipInCorso != null;

    void Awake()
    {
#if UNITY_EDITOR
        if ((clips == null || clips.Length == 0) && !string.IsNullOrEmpty(prefissoClip))
        {
            var lista = new List<AnimationClip>();
            foreach (var g in UnityEditor.AssetDatabase.FindAssets($"{prefissoClip} t:AnimationClip", new[] { TB_RegistratoreClip.Cartella }))
            {
                var c = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (c && c.name.StartsWith(prefissoClip)) lista.Add(c);
            }
            clips = lista.ToArray();
            if (clips.Length > 0) Debug.Log($"[TB] {name}: caricate {clips.Length} clip '{prefissoClip}*'");
        }
#endif
    }

    // Sceglie una clip a caso e mette l'avatar nella posa iniziale. Ritorna l'indice (-1 se nessuna).
    public int Prepara()
    {
        clipInCorso = null;
        if (clips == null || clips.Length == 0 || fantoccio == null) return -1;
        int i = Random.Range(0, clips.Length);
        clipPreparata = clips[i];
        if (!tChiusuraCache.ContainsKey(clipPreparata))
            tChiusuraCache[clipPreparata] = TrovaChiusura(clipPreparata);
        inPosa = true;                      // tiene la posa iniziale finché non parte il replay
        return i;
    }

    public void Avvia()
    {
        if (clipPreparata == null) return;
        clipInCorso = clipPreparata;
        tInizio = Time.realtimeSinceStartupAsDouble;
    }

    public void Ferma() { clipInCorso = null; inPosa = false; }

    void LateUpdate()
    {
        if (clipInCorso != null)
        {
            float t = (float)(Time.realtimeSinceStartupAsDouble - tInizio);
            Campiona(clipInCorso, Mathf.Min(t, clipInCorso.length));
        }
        else if (inPosa && clipPreparata != null)
            Campiona(clipPreparata, 0f);
    }

    // Solo dita: la clip muove soltanto ciò che sta sotto l'osso della mano (falangi, ganasce della pinzetta);
    // spalla, braccio, avambraccio e polso restano quelli tracciati dal vivo, così la mano è sempre dove la
    // tiene il partecipante (sul pulsante) e una clip vale per tutti i soggetti e per ogni posizione del tasto.
    void Campiona(AnimationClip clip, float t)
    {
        if (!soloDita || !mano) { clip.SampleAnimation(fantoccio, t); return; }
        if (bloccati == null)
        {
            var lista = new List<Transform>();
            foreach (var tr in fantoccio.GetComponentsInChildren<Transform>(true))
                if (tr == mano || !tr.IsChildOf(mano)) lista.Add(tr);
            bloccati = lista.ToArray();
            posBloccate = new Vector3[bloccati.Length];
            rotBloccate = new Quaternion[bloccati.Length];
            scaBloccate = new Vector3[bloccati.Length];
        }
        for (int i = 0; i < bloccati.Length; i++)
        {
            var tr = bloccati[i];
            posBloccate[i] = tr.localPosition; rotBloccate[i] = tr.localRotation; scaBloccate[i] = tr.localScale;
        }
        clip.SampleAnimation(fantoccio, t);
        for (int i = 0; i < bloccati.Length; i++)
        {
            var tr = bloccati[i];
            tr.localPosition = posBloccate[i]; tr.localRotation = rotBloccate[i]; tr.localScale = scaBloccate[i];
        }
    }

    Transform[] bloccati;
    Vector3[] posBloccate, scaBloccate;
    Quaternion[] rotBloccate;

    float TrovaChiusura(AnimationClip clip)
    {
        foreach (var e in clip.events)
            if (e.functionName == EventoChiusura) return e.time;

        if (puntaA == null || puntaB == null)
        {
            Debug.LogWarning($"[TB] {clip.name}: nessun evento di chiusura e punte non assegnate, chiusura assunta a metà clip");
            return clip.length * 0.5f;
        }
        for (float t = 0f; t <= clip.length; t += passoCampionamento)
        {
            clip.SampleAnimation(fantoccio, t);
            if (Vector3.Distance(puntaA.position, puntaB.position) < sogliaChiusura) return t;
        }
        Debug.LogWarning($"[TB] Nessuna chiusura trovata in {clip.name}: controlla la soglia");
        return clip.length * 0.5f;
    }
}
