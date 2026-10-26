using System.Collections.Generic;
using UnityEngine;

// Riproduce sul fantoccio (mano o mano+pinzetta) le clip registrate con TB_RegistratoreClip.
// L'istante di chiusura di ogni clip è trovato automaticamente: il primo istante in cui
// le due punte (pollice/indice o ganasce della pinzetta) scendono sotto la soglia.
// Il manager fa partire la clip in modo che la chiusura cada alla latenza voluta.
public class TB_Cinematica : MonoBehaviour
{
    [Tooltip("Radice del fantoccio: stessa gerarchia dell'oggetto registrato")]
    public GameObject fantoccio;
    public AnimationClip[] clips;

    [Header("Rilevamento chiusura nella clip")]
    public Transform puntaA;                // es. punta del pollice / ganascia 1 del fantoccio
    public Transform puntaB;                // es. punta dell'indice / ganascia 2
    public float sogliaChiusura = 0.015f;   // metri
    public float passoCampionamento = 1f / 240f;

    readonly Dictionary<AnimationClip, float> tChiusuraCache = new Dictionary<AnimationClip, float>();

    AnimationClip clipPreparata;
    AnimationClip clipInCorso;
    double tInizio;

    public double TempoAllaChiusura => clipPreparata != null ? tChiusuraCache[clipPreparata] : 0;
    public bool InRiproduzione => clipInCorso != null;

    // Sceglie una clip a caso e mette il fantoccio nella posa iniziale. Ritorna l'indice (-1 se nessuna).
    public int Prepara()
    {
        clipInCorso = null;
        if (clips == null || clips.Length == 0 || fantoccio == null) return -1;
        int i = Random.Range(0, clips.Length);
        clipPreparata = clips[i];
        if (!tChiusuraCache.ContainsKey(clipPreparata))
            tChiusuraCache[clipPreparata] = TrovaChiusura(clipPreparata);
        clipPreparata.SampleAnimation(fantoccio, 0f);
        return i;
    }

    public void Avvia()
    {
        if (clipPreparata == null) return;
        clipInCorso = clipPreparata;
        tInizio = Time.realtimeSinceStartupAsDouble;
    }

    public void Ferma() { clipInCorso = null; }

    void LateUpdate()
    {
        if (clipInCorso == null) return;
        float t = (float)(Time.realtimeSinceStartupAsDouble - tInizio);
        clipInCorso.SampleAnimation(fantoccio, Mathf.Min(t, clipInCorso.length));
    }

    float TrovaChiusura(AnimationClip clip)
    {
        if (puntaA == null || puntaB == null)
        {
            Debug.LogWarning($"[TB] Punte non assegnate su {name}: chiusura assunta a metà clip");
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
