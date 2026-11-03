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
    [Tooltip("Passivo: secondi prima della chiusura della clip in cui il tasto comincia a scendere con le dita")]
    public float durataCompressionePassiva = 0.08f;
    [Tooltip("Trial con frame persi tra azione e luce ripetuti al massimo per blocco")]
    public int maxRipetizioniPerBlocco = 10;
    [Tooltip("Se non ci sono ancora latenze attive registrate")]
    public Vector2 latenzaDefault = new Vector2(0.6f, 1.5f);

    [Header("Avvio")]
    public KeyCode tastoAvvio = KeyCode.S;
    public bool saltaFamiliarizzazione = false;
    [Tooltip("Ripresa di una sessione interrotta: parte da questo blocco della sequenza (1 = dall'inizio). " +
             "Se > 1 la familiarizzazione si salta e il log è un file nuovo con gli stessi codice/numero.")]
    [Min(1)] public int bloccoIniziale = 1;

    readonly Dictionary<TB_Effettore, List<double>> latenzeAttive = new Dictionary<TB_Effettore, List<double>>
    {
        { TB_Effettore.Mano, new List<double>() },
        { TB_Effettore.Tool, new List<double>() },
    };

    [Header("Questionario a fine blocco")]
    public bool questionarioFineBlocco = true;

    bool inCorso;
    bool sessioneFinita;                    // dopo "Fine. Grazie!" la schermata iniziale non lo sovrascrive

    // ---------------- stato per il pannello dello sperimentatore (F1) ----------------
    public bool InCorso => inCorso;
    public bool InProva => inProva;
    public bool SessioneFinita => sessioneFinita;
    public string Fase { get; private set; } = "In attesa dell'avvio";
    public bool AttendeX { get; private set; }
    public int BloccoCorrente { get; private set; }          // 1-based nella sequenza, 0 = familiarizzazione
    public int BlocchiTotali { get; private set; }
    public string CondizioneCorrente { get; private set; } = "";
    public int TrialCorrente { get; private set; }           // 1-based
    public int TrialTotali { get; private set; }
    public int TrialRiscaldamento { get; private set; }
    public int RipetutiBlocco { get; private set; }
    public int ItemCorrente { get; private set; }
    public int ItemTotali { get; private set; }
    public List<TB_Condizione> OrdineSessione { get; private set; } = new List<TB_Condizione>();
    public TB_Trial UltimoTrial { get; private set; }

    // comandi dal pannello
    bool avantiRichiesto, saltaBloccoRichiesto, saltaQuestionarioRichiesto;
    public bool pausaRichiesta;             // la sessione si ferma alla fine del trial in corso
    public void ComandoAvanti() => avantiRichiesto = true;
    public void ComandoSaltaBlocco() { if (inCorso) saltaBloccoRichiesto = true; }
    public void ComandoSaltaQuestionario() { if (inCorso) saltaQuestionarioRichiesto = true; }
    public bool PuoAvviare => !inCorso && LeggiSoggetto(out _, out _);
    public void ComandoAvvia() { if (PuoAvviare) StartCoroutine(Sessione()); }
    public void ComandoProva(int k) { if (!inCorso && k >= 0 && k < TB_Condizione.Tutte.Length) StartCoroutine(Prova(TB_Condizione.Tutte[k])); }

    // interrompe tutto subito (il log resta con i trial già scritti)
    public void ComandoTermina()
    {
        if (!inCorso) return;
        StopAllCoroutines();
        Debug.LogWarning($"[TB] Sessione interrotta dallo sperimentatore ({Fase}, blocco {BloccoCorrente}, trial {TrialCorrente})");
        if (slider) slider.Chiudi();
        if (mirino) mirino.SetActive(false);
        if (lampadina) { lampadina.Spegni(); lampadina.PremiTasto(false); lampadina.compressioneReplay = -1f; }
        if (cinematicaMano) cinematicaMano.Ferma();
        if (cinematicaTool) cinematicaTool.Ferma();
        if (pistone) { pistone.Ritira(); pistone.gameObject.SetActive(false); }
        if (swap) swap.enabled = true;
        inCorso = false; inProva = false; sessioneFinita = true;
        pausaRichiesta = false; AttendeX = false;
        Fase = "Interrotta";
        Testo("Sessione interrotta");
    }

    void Awake()
    {
        if (!GetComponent<TB_PannelloSperimentatore>()) gameObject.AddComponent<TB_PannelloSperimentatore>();
    }

    void Start()
    {
        PreparaScena(TB_Condizione.Tutte[0]);
        if (mirino) mirino.SetActive(false);
        Testo(TestoAvvio(out _));
    }

    void Update()
    {
        if (inCorso) return;
        // prova di un singolo blocco (niente log, niente dati soggetto): 1-5 = condizioni in TB_Condizione.Tutte
        for (int k = 0; k < TB_Condizione.Tutte.Length; k++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + k) || Input.GetKeyDown(KeyCode.Keypad1 + k))
            {
                StartCoroutine(Prova(TB_Condizione.Tutte[k]));
                return;
            }
        // codice e numero si possono compilare anche in Play: la schermata iniziale si aggiorna
        string msg = TestoAvvio(out bool valido);
        if (!sessioneFinita && istruzioni && istruzioni.text != msg) Testo(msg);
        if (valido && Avanti())
            StartCoroutine(Sessione());
    }

    public string TestoAvvioPannello => TestoAvvio(out _);

    // avvio e pause: X sul controller sinistro, oppure S / X / Invio da tastiera
    // (o il pulsante "Avanti" del pannello F1)
    bool Avanti()
    {
        if (avantiRichiesto) { avantiRichiesto = false; return true; }
        return TB_Input.XGiu || Input.GetKeyDown(tastoAvvio) || Input.GetKeyDown(KeyCode.X) ||
               Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    // Numero soggetto -> sequenza di Williams (controbilanciamento dei blocchi) e ordine della familiarizzazione
    static bool LeggiSoggetto(out string codice, out int numero)
    {
        var pi = ParticipantInfo.Instance;
        codice = pi.Codice_Soggetto == null ? "" : pi.Codice_Soggetto.Trim();
        numero = 0;
        return codice.Length > 0 && int.TryParse((pi.NumeroSoggetto ?? "").Trim(), out numero) && numero >= 1;
    }

    public static int Sequenza(int numero) => (numero - 1) % (2 * TB_Condizione.Tutte.Length) + 1;

    // dispari: prima la mano; pari: prima la pinzetta
    public static TB_Effettore[] OrdineFamiliarizzazione(int numero) =>
        numero % 2 == 1 ? new[] { TB_Effettore.Mano, TB_Effettore.Tool } : new[] { TB_Effettore.Tool, TB_Effettore.Mano };

    string TestoAvvio(out bool valido)
    {
        const string prova = "\n\nProva di un blocco (tastiera): 1 Mano attivo, 2 Mano passivo,\n3 Pinzetta attivo, 4 Pinzetta passivo, 5 Esterno";
        valido = LeggiSoggetto(out string codice, out int n);
        if (!valido)
            return "Compila Codice_Soggetto e NumeroSoggetto (intero da 1)\nnel componente ParticipantInfo, poi premi X" + prova;
        return $"Soggetto {codice} (n. {n}) - sequenza {Sequenza(n)}/{2 * TB_Condizione.Tutte.Length}\n" +
               string.Join(" > ", OrdineWilliams(n).Select(c => c.Nome)) +
               (bloccoIniziale > 1 ? $"\nRIPRESA dal blocco {Mathf.Min(bloccoIniziale, TB_Condizione.Tutte.Length)}: " +
                                     OrdineWilliams(n)[Mathf.Min(bloccoIniziale, TB_Condizione.Tutte.Length) - 1].Nome : "") +
               "\n\nPremi X per iniziare" + prova;
    }

    [Header("Prova (tasti 1-5 fuori sessione)")]
    public int trialProva = 6;
    bool inProva;

    // Blocco di prova di una condizione: stessi trial veri ma non scritti nel log
    IEnumerator Prova(TB_Condizione cond)
    {
        inCorso = true; inProva = true; sessioneFinita = false;
        if (swap) swap.enabled = false;
        Debug.Log($"[TB] Prova del blocco {cond.Nome}: {trialProva} trial, non salvati");
        OrdineSessione = new List<TB_Condizione> { cond };
        BloccoCorrente = 1; BlocchiTotali = 1; CondizioneCorrente = "PROVA " + cond.Nome;
        TrialCorrente = 0; TrialTotali = trialProva; TrialRiscaldamento = 0; RipetutiBlocco = 0; ItemTotali = 0;
        Fase = "Prova: istruzioni";
        yield return Pausa($"PROVA: {cond.Nome}\n{TestoBlocco(cond, 1, 1)}\n\nPremi X", cond);
        for (int i = 0; i < trialProva && !saltaBloccoRichiesto; i++)
        {
            var t = new TB_Trial { blocco = 0, indice = i, warmup = true, condizione = cond,
                                   delayNominaleMs = delaysMs[i % delaysMs.Length] };
            TrialCorrente = i + 1; Fase = "Prova: trial";
            yield return EseguiTrial(t);
            yield return PausaSperimentatore();
        }
        saltaBloccoRichiesto = false;
        if (swap) swap.enabled = true;
        Fase = "In attesa dell'avvio"; BloccoCorrente = 0; TrialCorrente = 0; TrialTotali = 0;
        inCorso = false; inProva = false;    // torna la schermata iniziale
    }

    // ---------------- sessione ----------------

    IEnumerator Sessione()
    {
        inCorso = true;
        if (swap) swap.enabled = false;       // durante la sessione l'avatar lo sceglie il manager
        LeggiSoggetto(out string soggetto, out int n);
        string numero = n.ToString();

        var ordine = OrdineWilliams(n);
        OrdineSessione = ordine; BlocchiTotali = ordine.Count;
        saltaBloccoRichiesto = saltaQuestionarioRichiesto = avantiRichiesto = false;
        logger.Apri(soggetto, numero, string.Join("-", ordine.Select(c => c.Nome)));
        Debug.Log($"[TB] Sessione: soggetto {soggetto} n. {n}, sequenza Williams {Sequenza(n)}: {string.Join(" > ", ordine.Select(c => c.Nome))}; " +
                  $"familiarizzazione {string.Join(" > ", OrdineFamiliarizzazione(n))}");

        int primo = Mathf.Clamp(bloccoIniziale, 1, ordine.Count) - 1;
        if (primo > 0) Debug.Log($"[TB] Ripresa dal blocco {primo + 1}/{ordine.Count} ({ordine[primo].Nome}), familiarizzazione saltata");
        if (!saltaFamiliarizzazione && primo == 0)
        {
            foreach (var eff in OrdineFamiliarizzazione(n))
            {
                var cond = new TB_Condizione(eff, TB_Agency.Attivo);
                BloccoCorrente = 0; CondizioneCorrente = "Familiarizzazione " + (eff == TB_Effettore.Mano ? "Mano" : "Pinzetta");
                TrialCorrente = 0; TrialTotali = trialFamiliarizzazione; TrialRiscaldamento = trialFamiliarizzazione; RipetutiBlocco = 0;
                Fase = "Familiarizzazione: istruzioni";
                yield return Pausa($"Familiarizzazione: {(eff == TB_Effettore.Mano ? "MANO" : "PINZETTA")}\n" +
                                   "Dopo che il mirino scompare, stringi il pulsante quando vuoi.\n" +
                                   "Poi indica quanti ms sono passati tra la tua azione e la luce.\n\nPremi X", cond);
                for (int i = 0; i < trialFamiliarizzazione && !saltaBloccoRichiesto; i++)
                {
                    var t = new TB_Trial { blocco = 0, indice = i, warmup = true, condizione = cond,
                                           delayNominaleMs = ArrotondaAlFrame(Random.Range(100, 1001)) };
                    TrialCorrente = i + 1; Fase = "Familiarizzazione: trial";
                    yield return EseguiTrial(t);
                    yield return PausaSperimentatore();
                }
                if (saltaBloccoRichiesto) Debug.LogWarning($"[TB] {CondizioneCorrente} saltata dallo sperimentatore al trial {TrialCorrente}");
                saltaBloccoRichiesto = false;
            }
        }

        for (int b = primo; b < ordine.Count; b++)
        {
            var cond = ordine[b];
            BloccoCorrente = b + 1; CondizioneCorrente = cond.Nome;
            TrialCorrente = 0; RipetutiBlocco = 0; ItemCorrente = 0; ItemTotali = 0;
            TrialTotali = warmupPerBlocco + delaysMs.Length * trialPerDelay; TrialRiscaldamento = warmupPerBlocco;
            Fase = "Blocco: istruzioni";
            yield return Pausa(TestoBlocco(cond, b + 1, ordine.Count), cond);
            var lista = ListaTrial(cond, b + 1);
            int ripetuti = 0;
            for (int i = 0; i < lista.Count && !saltaBloccoRichiesto; i++)
            {
                var t = lista[i];
                TrialCorrente = i + 1; TrialTotali = lista.Count; Fase = t.warmup ? "Blocco: riscaldamento" : "Blocco: trial";
                yield return EseguiTrial(t);
                yield return PausaSperimentatore();
                // frame persi tra azione e luce: il trial resta nel log (FramePersi > 0, da escludere)
                // e lo stesso delay viene ripetuto in un punto casuale del resto del blocco
                if (t.framePersi > 0 && !t.warmup && ripetuti < maxRipetizioniPerBlocco)
                {
                    ripetuti++;
                    var r = new TB_Trial { blocco = t.blocco, indice = lista.Count, warmup = false,
                                           condizione = t.condizione, delayNominaleMs = t.delayNominaleMs,
                                           ripetizione = t.ripetizione + 1 };
                    lista.Insert(Random.Range(i + 1, lista.Count + 1), r);
                    RipetutiBlocco = ripetuti; TrialTotali = lista.Count;
                }
            }
            if (saltaBloccoRichiesto) Debug.LogWarning($"[TB] Blocco {b + 1} ({cond.Nome}) saltato dallo sperimentatore al trial {TrialCorrente}/{lista.Count}");
            saltaBloccoRichiesto = false;

            if (questionarioFineBlocco) yield return Questionario(cond, b + 1, n);
        }

        Fase = "Fine";
        Testo("Fine. Grazie!");
        sessioneFinita = true;
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
        else if (passivo)
        {
            // la clip muove solo le dita: la mano (vera) deve essere già sul pulsante, aperta e ferma
            // pronta quando la mano è vicina al tasto e le dita non stringono; il pannello dice cosa manca
            const string invito = "Metti la mano aperta attorno al pulsante e lasciala ferma: le dita si muoveranno da sole";
            while (!sensore.ProntaPassivo)
            {
                Testo(invito + (!sensore.Tracciata ? "\n(mano non vista)" : !sensore.ManoAlTasto ? "\n(avvicina la mano al pulsante)" : "\n(apri un po' le dita)"));
                yield return null;
            }
            Testo(invito);
            yield return new WaitForSecondsRealtime(0.8f);
        }
        else
        {
            Testo("Tieni la mano destra ferma a riposo");
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
                // il tasto scende con le dita della clip nell'ultimo tratto prima della chiusura
                lampadina.compressioneReplay = Mathf.Clamp01((float)((now - (tEventoProgrammato - durataCompressionePassiva)) / durataCompressionePassiva));
                if (now >= tEventoProgrammato) { t.tEvento = now; lampadina.compressioneReplay = -1f; break; }
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

        UltimoTrial = t;
        if (!inProva) logger.Scrivi(t);   // i trial di prova non vanno nel log
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
        AttendeX = true;
        yield return new WaitUntil(Avanti);
        AttendeX = false;
        Testo("");
    }

    // pausa chiesta dallo sperimentatore dal pannello: si ferma tra un trial e l'altro
    IEnumerator PausaSperimentatore()
    {
        if (!pausaRichiesta || saltaBloccoRichiesto) yield break;
        string fase = Fase;
        Fase = "In pausa (sperimentatore)";
        Testo("Pausa");
        yield return new WaitUntil(() => !pausaRichiesta || saltaBloccoRichiesto);
        Testo("");
        Fase = fase;
    }

    // Questionario di fine blocco: item in ordine casuale riproducibile per soggetto e blocco, Likert 1-7 sullo slider
    IEnumerator Questionario(TB_Condizione cond, int blocco, int numeroSoggetto)
    {
        saltaQuestionarioRichiesto = false;
        var items = TB_Questionario.Ordine(numeroSoggetto, blocco);
        ItemTotali = items.Count; ItemCorrente = 0;
        Fase = "Questionario: istruzioni";
        yield return Pausa("Fine del blocco.\nOra qualche domanda su quello che hai provato in questo blocco.\n" +
                           $"Muovi lo slider con lo stick (da 1 = {TB_Questionario.AncoraMin.ToLower()}\na 7 = {TB_Questionario.AncoraMax.ToLower()}) " +
                           "e conferma con X.\n\nPremi X per iniziare", cond);
        for (int k = 0; k < items.Count && !saltaQuestionarioRichiesto; k++)
        {
            var it = items[k];
            string testo = TB_Questionario.Testo(it, cond);
            ItemCorrente = k + 1; Fase = "Questionario";
            Testo($"Domanda {k + 1}/{items.Count}\n\n\"{testo}\"\n\n1 = {TB_Questionario.AncoraMin}     7 = {TB_Questionario.AncoraMax}");
            yield return null;                       // la X che ha chiuso la domanda precedente non conferma questa
            slider.ApriLikert(TB_Questionario.Min, TB_Questionario.Max);
            double t0 = Time.realtimeSinceStartupAsDouble;
            yield return new WaitUntil(() => slider.Confermato || saltaQuestionarioRichiesto);
            if (!slider.Confermato) { slider.Chiudi(); break; }
            double rt = (Time.realtimeSinceStartupAsDouble - t0) * 1000.0;
            slider.Chiudi();
            logger.ScriviItem(blocco, cond, it.id, k + 1, slider.Valore, slider.ValoreIniziale, rt, testo);
            Testo("");
            yield return new WaitForSecondsRealtime(0.3f);
        }
        if (saltaQuestionarioRichiesto) Debug.LogWarning($"[TB] Questionario del blocco {blocco} interrotto dallo sperimentatore alla domanda {ItemCorrente}/{items.Count}");
        saltaQuestionarioRichiesto = false;
        ItemCorrente = 0; ItemTotali = 0;
        Testo("");
    }

    string TestoBlocco(TB_Condizione c, int b, int tot)
    {
        string corpo = c.agency switch
        {
            TB_Agency.Attivo => $"Usa la {(c.effettore == TB_Effettore.Mano ? "MANO" : "PINZETTA")}: dopo il mirino stringi il pulsante quando vuoi.",
            TB_Agency.Passivo => $"La {(c.effettore == TB_Effettore.Mano ? "MANO" : "PINZETTA")} stringerà il pulsante da sola. Tieni la mano aperta attorno al pulsante, rilassata e ferma: non stringere.",
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
