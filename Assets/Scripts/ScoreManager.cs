using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreManager : MonoBehaviour
{
    public int punteggio = 0;
    public TextMeshProUGUI punteggioText;

    public void AggiungiPunto()
    {
        punteggio++;
        punteggioText.text = "Punti: " + punteggio;
        Debug.Log("Punteggio: " + punteggio);
    }
}