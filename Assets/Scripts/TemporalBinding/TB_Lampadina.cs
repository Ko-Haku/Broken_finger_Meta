using UnityEngine;

// Lampadina (outcome) e pulsante (primo evento).
// Accensione: filamento emissivo + vetro leggermente caldo + luce puntiforme + suono.
// La luce si accende nel frame più vicino all'istante programmato (errore max mezzo frame);
// il suono è programmato sull'orologio audio (PlayScheduled) per lo stesso istante.
public class TB_Lampadina : MonoBehaviour
{
    [Header("Lampadina")]
    public Renderer filamento;
    public Renderer vetro;                   // opzionale: bagliore leggero del bulbo
    public Light luce;
    [ColorUsage(false, true)] public Color emissioneFilamento = new Color(1f, 0.55f, 0.18f) * 12f;
    [ColorUsage(false, true)] public Color emissioneVetro = new Color(1f, 0.7f, 0.4f) * 0.35f;
    public AudioSource suonoAccensione;

    [Header("Pulsante")]
    public Transform tasto;                  // parte mobile (origine alla sua base)
    public float corsaTasto = 0.006f;        // metri
    public AudioSource suonoClick;

    Vector3 posTastoRiposo;
    Material matFilamento, matVetro;
    double tProgrammato = -1;
    public double TAccensione { get; private set; } = -1;
    public bool Accesa { get; private set; }

    void Awake()
    {
        if (filamento) { matFilamento = filamento.material; matFilamento.EnableKeyword("_EMISSION"); }
        if (vetro) { matVetro = vetro.material; matVetro.EnableKeyword("_EMISSION"); }
        if (tasto) posTastoRiposo = tasto.localPosition;
        Spegni();
    }

    public void Programma(double tAss)
    {
        tProgrammato = tAss; TAccensione = -1;
        if (suonoAccensione)
        {
            double anticipo = tAss - Time.realtimeSinceStartupAsDouble;
            suonoAccensione.PlayScheduled(AudioSettings.dspTime + System.Math.Max(0.0, anticipo));
        }
    }

    public void PremiTasto(bool premuto)
    {
        if (tasto) tasto.localPosition = posTastoRiposo - (premuto ? Vector3.up * corsaTasto : Vector3.zero);
        if (premuto && suonoClick) suonoClick.Play();
    }

    void Update()
    {
        if (tProgrammato < 0 || Accesa) return;
        double now = Time.realtimeSinceStartupAsDouble;
        double halfFrame = Time.unscaledDeltaTime * 0.5;
        if (now + halfFrame >= tProgrammato)
        {
            Accendi();
            TAccensione = now;
            tProgrammato = -1;
        }
    }

    public void Accendi()
    {
        Accesa = true;
        if (matFilamento) matFilamento.SetColor("_EmissionColor", emissioneFilamento);
        if (matVetro) matVetro.SetColor("_EmissionColor", emissioneVetro);
        if (luce) luce.enabled = true;
    }

    public void Spegni()
    {
        Accesa = false;
        tProgrammato = -1;
        if (suonoAccensione && suonoAccensione.isPlaying) suonoAccensione.Stop();
        if (matFilamento) matFilamento.SetColor("_EmissionColor", Color.black);
        if (matVetro) matVetro.SetColor("_EmissionColor", Color.black);
        if (luce) luce.enabled = false;
    }

    // Prova in editor: tasto destro sul componente
    [ContextMenu("Prova accensione")] void ProvaAccendi() { Awake(); Accendi(); }
    [ContextMenu("Prova spegnimento")] void ProvaSpegni() { Awake(); Spegni(); }
}
