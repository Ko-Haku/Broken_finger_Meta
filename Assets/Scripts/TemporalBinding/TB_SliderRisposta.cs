using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Slider 1-1000 ms mosso con il thumbstick sinistro, conferma con X (come Kong et al. 2024, Exp. 2).
// Posizione iniziale casuale per evitare ancoraggi sistematici.
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

    float v;
    bool attivo;
    public bool Confermato { get; private set; }
    public int Valore => Mathf.RoundToInt(v);
    public int ValoreIniziale { get; private set; }

    void Awake() { Mostra(false); }

    public void Apri()
    {
        v = Random.Range(min, max + 1);
        ValoreIniziale = Mathf.RoundToInt(v);
        Confermato = false;
        attivo = true;
        Mostra(true);
        Aggiorna();
    }

    public void Chiudi() { attivo = false; Mostra(false); }

    void Mostra(bool on) { if (pannello) pannello.SetActive(on); }

    void Update()
    {
        if (!attivo) return;

        float x = TB_Input.StickX;
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
        if (slider) { slider.minValue = min; slider.maxValue = max; slider.value = v; }
        if (valore) valore.text = $"{Valore} ms";
    }
}
