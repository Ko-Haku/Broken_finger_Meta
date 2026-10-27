using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

// Paradigma di temporal binding con stima dell'intervallo (Kong et al. 2024; Engbert et al. 2007).
// 5 condizioni a blocchi: Mano/Tool x Attivo/Passivo + Esterno.
//  - Attivo: il soggetto chiude la presa (pinch) sul clicker quando vuole dopo il "via".
//  - Passivo: l'avatar riproduce una clip registrata (TB_RegistratoreClip -> TB_Cinematica), alla latenza
//    estratta dalla distribuzione di latenze attive del soggetto; la mano reale resta a riposo.
//  - Esterno: un pistone meccanico (TB_Pistone) preme il pulsante; avatar Mano a riposo.
// Un solo avatar visibile alla volta (TB_SwapEffettore): Mano oppure moncherino + tool.
// Outcome: la lampadina si accende dopo 150/450/750 ms; poi slider 1-1000 ms.
public class TB_ExperimentManager : MonoBehaviour
{
    [Header("Componenti")]
    public TB_PinchSensor sensore;
    public TB_Lampadina lampadina;
    public TB_SliderRisposta slider;
    public TB_Logger logger;
    public TB_Cinematica cinematicaMano;
    public TB_Cinematica cinematicaTool;

    [Header("Scena")]
    public TB_SwapEffettore swap;           // avatar Mano / Tool
    public TB_Pistone pistone;              // agente della condizione Esterno (visibile solo lì)

    [Header("UI")]
    public GameObject mirino;
    public TMP_Text istruzioni;

    [Header("Disegno")]
    public int[] delaysMs = { 150, 450, 750 };
    public int trialPerDelay = 10;
    public int warmupPerBlocco = 3;          // uno per delay, scartati in analisi
    public int trialFamiliarizzazione = 12;  // per effettore
    public Vector2 durataMirino = new Vector2(1f, 2.5f);
    public float durataLuce = 1f;
    public Vector2 iti = new Vector2(0.5f, 1f);
    public float timeoutAzione = 8f;
    [Tooltip("Trial con frame persi tra azione e luce ripetuti al massimo per blocco")]
    public int maxRipetizioniPerBlocco = 10;
    [Tooltip("Se non ci sono ancora latenze attive registrate")]
    public Vector2 latenzaDefault = new Vector2(0.6f, 1.5f);

    [Header("Avvio")]
    public KeyCode tastoAvvio = KeyCode.S;
    public bool saltaFamiliarizzazione = false;

    readonly Dictionary<TB_Effettore, List<double>> latenzeAttive = new Dictionary<TB_Effettore, List<double>>
    {
        { TB_Effettore.Mano, new List<double>() },
        { TB_Effettore.Tool, new List<double>() },
    };

    bool inCorso;

    void Start()
    {
        PreparaScena(TB_Condizione.Tutte[0]);
        if (mirino) mirino.SetActive(false);
        Testo("Premi X per iniziare");
    }

    void Update()
    {
        if (!inCorso && (Input.GetKeyDown(tastoAvvio) || TB_Input.XGiu))
            StartCoroutine(Sessione());
    }

    // ---------------- sessione ----------------

    IEnumerator Sessione()
    {
        inCorso = true;
        if (swap) swap.enabled = false;       // durante la sessione l'avatar lo sceglie il manager
        var pi = ParticipantInfo.Instance;
        string soggetto = string.IsNullOrWhiteSpace(pi.Codice_Soggetto) ? "NA" : pi.Codice_Soggetto.Trim();
        string numero = string.IsNullOrWhiteSpace(pi.NumeroSoggetto) ? "0" : pi.NumeroSoggetto.Trim();
        int.TryParse(numero, out int n);

        var ordine = OrdineWilliams(n);
        logger.Apri(soggetto, numero, string.Join("-", ordine.Select(c => c.Nome)));

        if (!saltaFamiliarizzazione)
        {
            foreach (var eff in new[] { TB_Effettore.Mano, TB_Effettore.Tool })
            {
                var cond = new TB_Condizione(eff, TB_Agency.Attivo);
                yield return Pausa($"Familiarizzazione: {(eff == TB_Effettore.Mano ? "MANO" : "PINZETTA")}\n" +
                                   "Dopo che il mirino scompare, stringi il pulsante quando vuoi.\n" +
                                   "Poi indica quanti ms sono passati tra la tua azione e la luce.\n\nPremi X", cond);
                for (int i = 0; i < trialFamiliarizzazione; i++)
                {
                    var t = new TB_Trial { blocco = 0, indice = i, warmup = true, condizione = cond,
                                           delayNominaleMs = ArrotondaAlFrame(Random.Range(100, 1001)) };
                    yield return EseguiTrial(t);
                }
            }
        }

        for (int b = 0; b < ordine.Count; b++)
        {
            var cond = ordine[b];
            yield return Pausa(TestoBlocco(cond, b + 1, ordine.Count), cond);
            var lista = ListaTrial(cond, b + 1);
            int ripetuti = 0;
            for (int i = 0; i < lista.Count; i++)
            {
                var t = lista[i];
                yield return EseguiTrial(t);
                // frame persi tra azione e luce: il trial resta nel log (FramePersi > 0, da escludere)
                // e lo stesso delay viene ripetuto in un punto casuale del resto del blocco
                if (t.framePersi > 0 && !t.warmup && ripetuti < maxRipetizioniPerBlocco)
                {
                    ripetuti++;
                    var r = new TB_Trial { blocco = t.blocco, indice = lista.Count, warmup = false,
                                           condizione = t.condizione, delayNominaleMs = t.delayNominaleMs,
                                           ripetizione = t.ripetizione + 1 };
                    lista.Insert(Random.Range(i + 1, lista.Count + 1), r);
                }
            }
        }

        Testo("Fine. Grazie!");
        if (swap) swap.enabled = true;
        inCorso = false;
    }

    List<TB_Trial> ListaTrial(TB_Condizione cond, int blocco)
    {
        var warm = delaysMs.OrderBy(_ => Random.value).Take(warmupPerBlocco).ToList();
        var main = new List<int>();
        foreach (var d in delaysMs) for (int k = 0; k < trialPerDelay; k++) main.Add(d);
        main = main.OrderBy(_ => Random.value).ToList();

        var res = new List<TB_Trial>();
        int i = 0;
        foreach (var d in warm) res.Add(new TB_Trial { blocco = blocco, indice = i++, warmup = true, condizione = cond, delayNominaleMs = d });
        foreach (var d in main) res.Add(new TB_Trial { blocco = blocco, indice = i++, warmup = false, condizione = cond, delayNominaleMs = d });
        return res;
    }

    // ---------------- trial ----------------

    IEnumerator EseguiTrial(TB_Trial t)
    {
        var c = t.condizione;
        var cin = c.effettore == TB_Effettore.Tool ? cinematicaTool : cinematicaMano;
        bool passivo = c.agency == TB_Agency.Passivo;
        bool attivo = c.agency == TB_Agency.Attivo;
        bool esterno = c.agency == TB_Agency.Esterno;

        lampadina.Spegni();
        lampadina.PremiTasto(false);

        if (passivo)
        {
            t.clipIndex = cin.Prepara();
            if (t.clipIndex < 0) Debug.LogWarning($"[TB] Nessuna clip registrata per {c.effettore} (TB_Cinematica.clips): l'avatar resta fermo");
        }
        PreparaScena(c);

        // pronto
        if (attivo)
        {
            Testo("Apri pollice e indice attorno al pulsante");
            yield return new WaitUntil(() => sensore.Pronta);
        }
        else
        {
            Testo(passivo ? "Tieni la mano destra ferma a riposo e guarda" : "Tieni la mano destra ferma a riposo");
            yield return new WaitForSecondsRealtime(0.8f);
        }
        Testo("");

        // mirino
        double now = Time.realtimeSinceStartupAsDouble;
        double fix = Random.Range(durataMirino.x, durataMirino.y);
        t.tFixOff = now + fix;

        // istante del primo evento programmato per passivo/esterno, agganciato alle latenze attive
        double latenza = EstraiLatenza(c.effettore);
        double tEventoProgrammato = t.tFixOff + latenza;
        // il replay deve poter partire dal suo primo frame: mai in ritardo sulla chiusura
        if (passivo) tEventoProgrammato = System.Math.Max(tEventoProgrammato, now + cin.TempoAllaChiusura);
        double tAvvioReplay = passivo ? tEventoProgrammato - cin.TempoAllaChiusura : double.MaxValue;
        // esterno: l'asta del pistone tocca il tasto esattamente all'istante programmato
        if (esterno && pistone)
        {
            tEventoProgrammato = System.Math.Max(tEventoProgrammato, now + pistone.durataDiscesa);
            pistone.Programma(tEventoProgrammato);
        }

        if (mirino) mirino.SetActive(true);

        float percorsoMano = 0f;
        Vector3 ultimoPolso = sensore.PosizionePolso;
        bool replayAvviato = false;
        bool anticipato = false;

        // attesa del primo evento
        while (true)
        {
            now = Time.realtimeSinceStartupAsDouble;
            if (mirino && mirino.activeSelf && now >= t.tFixOff) mirino.SetActive(false);

            if (!attivo && sensore.Tracciata)
            {
                percorsoMano += Vector3.Distance(sensore.PosizionePolso, ultimoPolso);
                ultimoPolso = sensore.PosizionePolso;
            }

            if (attivo)
            {
                if (sensore.ChiusuraQuestoFrame)
                {
                    if (now < t.tFixOff) { anticipato = true; break; }
                    t.tEvento = now;
                    break;
                }
                if (now - t.tFixOff > timeoutAzione) { anticipato = true; break; }
            }
            else if (passivo)
            {
                if (!replayAvviato && now >= tAvvioReplay) { cin.Avvia(); replayAvviato = true; }
                if (now >= tEventoProgrammato) { t.tEvento = now; break; }
            }
            else
            {
                if (now >= tEventoProgrammato) { t.tEvento = now; break; }
            }
            yield return null;
        }

        if (anticipato)
        {
            if (mirino) mirino.SetActive(false);
            Testo("Aspetta che il mirino scompaia, poi agisci");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return EseguiTrial(t);      // ripeti lo stesso trial
            yield break;
        }

        // primo evento -> outcome
        // contatore dei frame persi: comprende il frame dell'evento (se è arrivato in ritardo,
        // la chiusura è stata vista in ritardo)
        var timing = TB_Timing.Instance;
        long persiInizio = timing ? timing.FramePersiTotali - timing.FramePersiUltimoFrame : 0;
        lampadina.PremiTasto(true);
        lampadina.Programma(t.tEvento + t.delayNominaleMs / 1000.0);
        if (attivo)
        {
            latenzeAttive[c.effettore].Add(t.tEvento - t.tFixOff);
        }

        yield return new WaitUntil(() => lampadina.TAccensione > 0);
        t.tLuce = lampadina.TAccensione;
        // il frame della luce è già contato (TB_Timing gira prima di tutti gli Update)
        t.framePersi = timing ? (int)(timing.FramePersiTotali - persiInizio) : 0;

        double fineLuce = t.tLuce + durataLuce;
        while (Time.realtimeSinceStartupAsDouble < fineLuce)
        {
            if (!attivo && sensore.Tracciata)
            {
                percorsoMano += Vector3.Distance(sensore.PosizionePolso, ultimoPolso);
                ultimoPolso = sensore.PosizionePolso;
            }
            yield return null;
        }
        t.movimentoManoRealeMm = attivo ? 0f : percorsoMano * 1000f;

        lampadina.Spegni();
        lampadina.PremiTasto(false);
        cin.Ferma();
        if (pistone) pistone.Ritira();

        // risposta
        Testo("Quanti ms tra il primo evento e la luce?");
        slider.Apri();
        t.sliderStart = slider.ValoreIniziale;
        t.tSliderOn = Time.realtimeSinceStartupAsDouble;
        yield return new WaitUntil(() => slider.Confermato);
        t.tRisposta = Time.realtimeSinceStartupAsDouble;
        t.stimaMs = slider.Valore;
        slider.Chiudi();
        Testo("");

        logger.Scrivi(t);
        yield return new WaitForSecondsRealtime(Random.Range(iti.x, iti.y));
    }

    double EstraiLatenza(TB_Effettore eff)
    {
        List<double> pool = eff == TB_Effettore.Nessuno
            ? latenzeAttive.Values.SelectMany(l => l).ToList()
            : latenzeAttive[eff];
        if (pool.Count == 0) return Random.Range(latenzaDefault.x, latenzaDefault.y);
        return pool[Random.Range(0, pool.Count)];
    }

    // ---------------- helpers ----------------

    static int ArrotondaAlFrame(int ms) => TB_Timing.Instance ? TB_Timing.Instance.ArrotondaMs(ms) : ms;

    // L'avatar resta sempre visibile (embodiment): Mano per Mano ed Esterno, moncherino + tool per Tool.
    void PreparaScena(TB_Condizione c)
    {
        if (swap) swap.Imposta(c.effettore != TB_Effettore.Tool);
        if (pistone) pistone.gameObject.SetActive(c.agency == TB_Agency.Esterno);
    }

    void Testo(string s) { if (istruzioni) istruzioni.text = s; }

    IEnumerator Pausa(string msg, TB_Condizione prossima)
    {
        PreparaScena(prossima);
        Testo(msg);
        yield return null;
        yield return new WaitUntil(() => Input.GetKeyDown(tastoAvvio) ||
            TB_Input.XGiu);
        Testo("");
    }

    string TestoBlocco(TB_Condizione c, int b, int tot)
    {
        string corpo = c.agency switch
        {
            TB_Agency.Attivo => $"Usa la {(c.effettore == TB_Effettore.Mano ? "MANO" : "PINZETTA")}: dopo il mirino stringi il pulsante quando vuoi.",
            TB_Agency.Passivo => $"La {(c.effettore == TB_Effettore.Mano ? "MANO" : "PINZETTA")} si muoverà da sola. Tieni la mano destra ferma a riposo.",
            _ => "Il pulsante si abbasserà da solo. Tieni la mano destra ferma a riposo.",
        };
        return $"Blocco {b}/{tot}\n{corpo}\nStima i ms tra il primo evento e la luce.\n\nPremi X";
    }

    // Williams design per 5 condizioni (n dispari -> 10 sequenze bilanciate per effetti di trasporto).
    public static List<TB_Condizione> OrdineWilliams(int numeroSoggetto)
    {
        int n = TB_Condizione.Tutte.Length;
        // riga base: 0, 1, n-1, 2, n-2, ...
        var base0 = new int[n];
        base0[0] = 0;
        for (int i = 1, a = 1, z = n - 1; i < n; i++) base0[i] = i % 2 == 1 ? a++ : z--;

        int seq = ((numeroSoggetto - 1) % (2 * n) + 2 * n) % (2 * n);
        int riga = seq % n;
        var r = base0.Select(x => (x + riga) % n).ToArray();
        if (seq >= n) System.Array.Reverse(r);
        return r.Select(i => TB_Condizione.Tutte[i]).ToList();
    }
}
