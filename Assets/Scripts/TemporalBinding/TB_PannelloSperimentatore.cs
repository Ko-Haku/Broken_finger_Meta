using System.Linq;
using UnityEngine;

// Pannello dello sperimentatore per il temporal binding: si apre/chiude con F1 e compare solo nella Game view
// (e nella finestra del PC in una build), mai nel visore. Mostra soggetto, blocco, trial e questionario in corso
// e ha i pulsanti per avviare, andare avanti (= X del partecipante), mettere in pausa, saltare e interrompere.
// Lo aggiunge da solo TB_ExperimentManager in Awake.
[RequireComponent(typeof(TB_ExperimentManager))]
public class TB_PannelloSperimentatore : MonoBehaviour
{
    public KeyCode tasto = KeyCode.F1;
    public bool visibile = true;
    public float scala = 1f;

    TB_ExperimentManager m;
    Vector2 scroll;
    bool confermaTermina;
    GUIStyle titolo, grande, piccolo, box;

    void Awake() => m = GetComponent<TB_ExperimentManager>();

    void Update()
    {
        if (Input.GetKeyDown(tasto)) { visibile = !visibile; confermaTermina = false; }
    }

    void Stili()
    {
        if (titolo != null) return;
        titolo = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        grande = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = true };
        piccolo = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = true };
        box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
    }

    void OnGUI()
    {
        if (!m) return;
        if (!visibile)
        {
            GUI.Label(new Rect(8, 4, 300, 20), "F1: pannello sperimentatore");
            return;
        }
        Stili();
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scala);
        float w = 380f, h = Mathf.Min(Screen.height / scala - 16f, 720f);
        GUILayout.BeginArea(new Rect(8, 8, w, h), box);
        scroll = GUILayout.BeginScrollView(scroll);

        GUILayout.Label("Temporal binding - sperimentatore (F1)", titolo);
        Soggetto();
        GUILayout.Space(6);
        Stato();
        GUILayout.Space(6);
        Comandi();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void Soggetto()
    {
        var pi = ParticipantInfo.Instance;
        bool modificabile = !m.InCorso;
        GUILayout.BeginHorizontal();
        GUILayout.Label("Codice", GUILayout.Width(52));
        GUI.enabled = modificabile;
        pi.Codice_Soggetto = GUILayout.TextField(pi.Codice_Soggetto ?? "", GUILayout.Width(120));
        GUILayout.Label("N.", GUILayout.Width(20));
        pi.NumeroSoggetto = GUILayout.TextField(pi.NumeroSoggetto ?? "", GUILayout.Width(50));
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        if (int.TryParse((pi.NumeroSoggetto ?? "").Trim(), out int n) && n >= 1)
        {
            var ordine = m.InCorso && !m.InProva ? m.OrdineSessione : TB_ExperimentManager.OrdineWilliams(n);
            GUILayout.Label($"Sequenza Williams {TB_ExperimentManager.Sequenza(n)}/{2 * TB_Condizione.Tutte.Length}, " +
                            $"familiarizzazione {string.Join(" > ", TB_ExperimentManager.OrdineFamiliarizzazione(n).Select(e => e == TB_Effettore.Mano ? "Mano" : "Pinzetta"))}", piccolo);
            string s = "";
            for (int i = 0; i < ordine.Count; i++)
            {
                string nome = $"{i + 1}. {ordine[i].Nome}";
                bool corrente = m.InCorso && !m.InProva && m.BloccoCorrente == i + 1;
                bool fatto = m.InCorso && !m.InProva && m.BloccoCorrente > i + 1;
                bool saltato = !m.InCorso && i + 1 < m.bloccoIniziale;
                s += corrente ? $"<b><color=#ffd34d>{nome}</color></b>" : fatto || saltato ? $"<color=#888888>{nome}</color>" : nome;
                if (i < ordine.Count - 1) s += "   ";
            }
            GUILayout.Label(s, piccolo);
        }
        else GUILayout.Label("<color=#ff7070>Numero soggetto mancante (intero da 1)</color>", piccolo);

        GUI.enabled = !m.InCorso;
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Parti dal blocco: {m.bloccoIniziale}" + (m.bloccoIniziale > 1 ? " (ripresa, niente familiarizzazione)" : ""), piccolo);
        if (GUILayout.Button("-", GUILayout.Width(24))) m.bloccoIniziale = Mathf.Max(1, m.bloccoIniziale - 1);
        if (GUILayout.Button("+", GUILayout.Width(24))) m.bloccoIniziale = Mathf.Min(TB_Condizione.Tutte.Length, m.bloccoIniziale + 1);
        GUILayout.EndHorizontal();
        m.questionarioFineBlocco = GUILayout.Toggle(m.questionarioFineBlocco, " Questionario a fine blocco");
        GUI.enabled = true;
    }

    void Stato()
    {
        string fase = m.Fase + (m.AttendeX ? "  <color=#ffd34d>(attende X)</color>" : "") +
                      (m.pausaRichiesta && m.Fase != "In pausa (sperimentatore)" ? "  <color=#ffd34d>(pausa a fine trial)</color>" : "");
        GUILayout.Label($"<b>{fase}</b>", grande);
        if (!m.InCorso) return;

        string blocco = m.BloccoCorrente > 0 ? $"Blocco {m.BloccoCorrente}/{m.BlocchiTotali}: " : "";
        GUILayout.Label($"{blocco}<b>{m.CondizioneCorrente}</b>", grande);

        if (m.ItemTotali > 0)
            GUILayout.Label($"Questionario: domanda {m.ItemCorrente}/{m.ItemTotali}", grande);
        else if (m.TrialTotali > 0)
        {
            int validi = Mathf.Max(0, m.TrialCorrente - m.TrialRiscaldamento);
            string riscaldamento = m.TrialCorrente <= m.TrialRiscaldamento && m.TrialRiscaldamento > 0 && m.BloccoCorrente > 0 ? " (riscaldamento)" : "";
            GUILayout.Label($"Trial {m.TrialCorrente}/{m.TrialTotali}{riscaldamento}", grande);
            if (m.BloccoCorrente > 0)
                GUILayout.Label($"riscaldamento {m.TrialRiscaldamento}, ripetuti per frame persi {m.RipetutiBlocco}/{m.maxRipetizioniPerBlocco}", piccolo);
        }

        var t = m.UltimoTrial;
        if (t != null && t.stimaMs >= 0)
            GUILayout.Label($"Ultimo trial: ritardo {t.delayNominaleMs} ms (effettivo {t.DelayEffettivoMs:F0}), stima {t.stimaMs} ms, " +
                            $"frame persi {t.framePersi}" + (t.condizione.agency != TB_Agency.Attivo ? $", mano reale {t.movimentoManoRealeMm:F0} mm" : ""), piccolo);
        GUILayout.Label($"{1f / Mathf.Max(1e-4f, Time.smoothDeltaTime):F0} fps", piccolo);
    }

    void Comandi()
    {
        if (!m.InCorso)
        {
            GUI.enabled = m.PuoAvviare;
            if (GUILayout.Button(m.bloccoIniziale > 1 ? $"Avvia sessione dal blocco {m.bloccoIniziale}" : "Avvia sessione", GUILayout.Height(30))) m.ComandoAvvia();
            GUI.enabled = true;
            GUILayout.Label("Prova di un blocco (non salvata):", piccolo);
            GUILayout.BeginHorizontal();
            string[] nomi = { "Mano att.", "Mano pass.", "Pinz. att.", "Pinz. pass.", "Esterno" };
            for (int k = 0; k < TB_Condizione.Tutte.Length; k++)
                if (GUILayout.Button(nomi[k])) m.ComandoProva(k);
            GUILayout.EndHorizontal();
            return;
        }

        GUI.enabled = m.AttendeX;
        if (GUILayout.Button("Avanti (come X del partecipante)", GUILayout.Height(28))) m.ComandoAvanti();
        GUI.enabled = true;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(m.pausaRichiesta ? "Riprendi" : "Pausa a fine trial", GUILayout.Height(26))) m.pausaRichiesta = !m.pausaRichiesta;
        GUI.enabled = m.ItemTotali == 0;
        if (GUILayout.Button(m.InProva ? "Ferma la prova" : "Salta il resto del blocco", GUILayout.Height(26))) m.ComandoSaltaBlocco();
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUI.enabled = m.ItemTotali > 0;
        if (GUILayout.Button("Salta il resto del questionario")) m.ComandoSaltaQuestionario();
        GUI.enabled = true;

        GUILayout.Space(4);
        if (!confermaTermina)
        {
            if (GUILayout.Button("Interrompi la sessione...")) confermaTermina = true;
        }
        else
        {
            GUILayout.Label("<color=#ff7070>Interrompere subito? I trial già fatti restano nel log.</color>", piccolo);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sì, interrompi")) { m.ComandoTermina(); confermaTermina = false; }
            if (GUILayout.Button("Annulla")) confermaTermina = false;
            GUILayout.EndHorizontal();
        }
    }
}
