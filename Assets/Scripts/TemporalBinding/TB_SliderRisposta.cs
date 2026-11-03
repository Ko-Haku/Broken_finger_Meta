using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Slider 1-1000 ms mosso con il thumbstick sinistro, conferma con X (come Kong et al. 2024, Exp. 2).
// Posizione iniziale casuale per evitare ancoraggi sistematici.
// ApriLikert: stessa UI per il questionario (scala 1-7 a scatti, uno scatto per spinta dello stick).
public class TB_SliderRisposta : MonoBehaviour
{
    public GameObject pannello;
    public Slider slider;
    public TMP_Text valore;

    public int min = 1;
    public int max = 1000;
    [Tooltip("ms/s a deflessione piena")]
    public float velocitaMax = 600f;
    [Tooltip("Esponente: >1 dà più controllo fine vicino al centro dello stick")]
    public float curva = 2f;

    [Header("Debug tastiera")]
    public KeyCode sinistra = KeyCode.LeftArrow;
    public KeyCode destra = KeyCode.RightArrow;
    public KeyCode conferma = KeyCode.Return;

    [Header("Likert")]
    [Tooltip("Deflessione dello stick che fa uno scatto; tenendolo si ripete")]
    public float sogliaScatto = 0.5f;
    public float ripetizioneScatto = 0.35f;

    float v;
    bool attivo;
    bool likert; int likMin, likMax;
    float prossimoScatto; int ultimaDirezione;
    public bool Confermato { get; private set; }
    public int Valore => Mathf.RoundToInt(v);
    public int ValoreIniziale { get; private set; }

    TMP_Text etichettaMin, etichettaMax;    // scritte "1" e "1000" agli estremi, trovate nel pannello

    void Awake()
    {
        if (pannello)
            foreach (var t in pannello.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t == valore) continue;
                if (t.text.Trim() == min.ToString()) etichettaMin = t;
                else if (t.text.Trim() == max.ToString()) etichettaMax = t;
            }
        Mostra(false);
    }

    public void Apri()
    {
        likert = false;
        v = Random.Range(min, max + 1);
        ValoreIniziale = Mathf.RoundToInt(v);
        Confermato = false;
        attivo = true;
        Mostra(true);
        Aggiorna();
    }

    // scala Likert: posizione iniziale casuale come per i ms (niente risposta di default al centro)
    public void ApriLikert(int da, int a)
    {
        likert = true; likMin = da; likMax = a;
        v = Random.Range(da, a + 1);
        ValoreIniziale = Mathf.RoundToInt(v);
        Confermato = false;
        attivo = true;
        ultimaDirezione = 0;
        Mostra(true);
        Aggiorna();
    }

    public void Chiudi() { attivo = false; Mostra(false); }

    void Mostra(bool on) { if (pannello) pannello.SetActive(on); }

    void Update()
    {
        if (!attivo) return;

        float x = TB_Input.StickX;
        if (likert)
        {
            int dir = Mathf.Abs(x) >= sogliaScatto ? (int)Mathf.Sign(x) : 0;
            int passo = 0;
            if (dir != 0 && (dir != ultimaDirezione || Time.unscaledTime >= prossimoScatto))
            {
                passo = dir; prossimoScatto = Time.unscaledTime + ripetizioneScatto;
            }
            ultimaDirezione = dir;
            if (Input.GetKeyDown(sinistra)) passo = -1;
            if (Input.GetKeyDown(destra)) passo = 1;
            v = Mathf.Clamp(Mathf.Round(v) + passo, likMin, likMax);
            Aggiorna();
            if (TB_Input.XGiu || Input.GetKeyDown(conferma) || Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Confermato = true;
                attivo = false;
            }
            return;
        }
        if (Input.GetKey(sinistra)) x = -1f;
        if (Input.GetKey(destra)) x = 1f;

        v += Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), curva) * velocitaMax * Time.unscaledDeltaTime;
        v = Mathf.Clamp(v, min, max);
        Aggiorna();

        if (TB_Input.XGiu || Input.GetKeyDown(conferma) || Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Confermato = true;
            attivo = false;
        }
    }

    void Aggiorna()
    {
        if (slider)
        {
            slider.wholeNumbers = likert;
            slider.minValue = likert ? likMin : min; slider.maxValue = likert ? likMax : max; slider.value = v;
        }
        if (etichettaMin) etichettaMin.text = (likert ? likMin : min).ToString();
        if (etichettaMax) etichettaMax.text = (likert ? likMax : max).ToString();
        if (valore) valore.text = likert ? $"{Valore}" : $"{Valore} ms";
    }
}
