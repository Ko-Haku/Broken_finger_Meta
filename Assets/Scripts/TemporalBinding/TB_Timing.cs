using UnityEngine;

// Stabilità temporale del paradigma.
//  - Blocca il refresh del visore a una frequenza in cui i ritardi sono multipli interi del frame
//    (80 Hz: 150/450/750 ms = 12/36/60 frame).
//  - Livelli CPU/GPU alti e fissi, niente risoluzione dinamica.
//  - Conta i frame persi: un frame che dura più di 1.5 intervalli nominali è un frame saltato
//    (il compositor ha ripresentato l'immagine precedente). L'ExperimentManager li conta tra
//    azione e luce e ripete i trial "sporchi".
// In editor senza visore la frequenza resta quella del monitor: il conteggio funziona lo stesso.
[DefaultExecutionOrder(-1000)]
public class TB_Timing : MonoBehaviour
{
    public static TB_Timing Instance { get; private set; }

    [Tooltip("Hz. 80 rende 150/450/750 ms multipli interi del frame (12/36/60).")]
    public float frequenzaDesiderata = 80f;
    public OVRPlugin.ProcessorPerformanceLevel livelloCpu = OVRPlugin.ProcessorPerformanceLevel.SustainedHigh;
    public OVRPlugin.ProcessorPerformanceLevel livelloGpu = OVRPlugin.ProcessorPerformanceLevel.SustainedHigh;
    [Tooltip("Durata oltre la quale un frame conta come perso, in intervalli nominali")]
    public float sogliaFramePerso = 1.5f;

    public float FrequenzaHz { get; private set; }
    public double Intervallo => 1.0 / Mathf.Max(1f, FrequenzaHz);
    public long FramePersiTotali { get; private set; }
    public int FramePersiUltimoFrame { get; private set; }

    void Awake()
    {
        Instance = this;
        Applica();
    }

    void Applica()
    {
        var disponibili = OVRPlugin.systemDisplayFrequenciesAvailable;
        if (disponibili != null && System.Array.IndexOf(disponibili, frequenzaDesiderata) >= 0)
            OVRPlugin.systemDisplayFrequency = frequenzaDesiderata;
        else if (disponibili != null && disponibili.Length > 0)
            Debug.LogWarning($"[TB_Timing] {frequenzaDesiderata} Hz non disponibile ({string.Join(", ", disponibili)} Hz)");

        OVRPlugin.suggestedCpuPerfLevel = livelloCpu;
        OVRPlugin.suggestedGpuPerfLevel = livelloGpu;
        if (OVRManager.instance) OVRManager.instance.enableDynamicResolution = false;

        float hz = OVRPlugin.systemDisplayFrequency;
        if (hz <= 1f) hz = (float)Screen.currentResolution.refreshRateRatio.value;   // editor senza visore
        FrequenzaHz = hz;
        Debug.Log($"[TB_Timing] refresh {FrequenzaHz:F1} Hz (frame {Intervallo * 1000:F2} ms), CPU {livelloCpu}, GPU {livelloGpu}");
    }

    void Update()
    {
        // la frequenza può cambiare (Link, sistema): la si rilegge, senza costo
        float hz = OVRPlugin.systemDisplayFrequency;
        if (hz > 1f && Mathf.Abs(hz - FrequenzaHz) > 0.5f)
        {
            Debug.LogWarning($"[TB_Timing] refresh cambiato: {FrequenzaHz:F1} -> {hz:F1} Hz");
            FrequenzaHz = hz;
        }

        double dt = Time.unscaledDeltaTime;
        FramePersiUltimoFrame = dt > sogliaFramePerso * Intervallo
            ? Mathf.Max(1, Mathf.RoundToInt((float)(dt / Intervallo)) - 1) : 0;
        FramePersiTotali += FramePersiUltimoFrame;

        // riepilogo periodico in console (solo se qualcosa è andato perso)
        if (Time.realtimeSinceStartup >= prossimoRiepilogo)
        {
            long persi = FramePersiTotali - persiAlRiepilogo;
            if (persi > 0) Debug.LogWarning($"[TB_Timing] {persi} frame persi negli ultimi {periodoRiepilogo:F0} s a {FrequenzaHz:F0} Hz");
            persiAlRiepilogo = FramePersiTotali;
            prossimoRiepilogo = Time.realtimeSinceStartup + periodoRiepilogo;
        }
    }

    const float periodoRiepilogo = 30f;
    float prossimoRiepilogo = periodoRiepilogo;
    long persiAlRiepilogo;

    // Arrotonda un ritardo al numero intero di frame più vicino (per il log e la familiarizzazione)
    public int ArrotondaMs(int ms) => Mathf.RoundToInt(Mathf.Round((float)(ms / 1000.0 / Intervallo)) * (float)Intervallo * 1000f);
}
