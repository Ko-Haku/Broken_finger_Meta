using UnityEngine;

public class CambiaColore : MonoBehaviour
{
    private Material materiale;

    void Awake()
    {
        // Istanzia un materiale nuovo per non modificare il materiale condiviso
        materiale = GetComponent<MeshRenderer>().material;
    }

    public void ImpostaColoreVerde()
    {
        materiale.color = Color.green;
    }

    public void ImpostaColoreBianco()
    {
        materiale.color = Color.white;
    }

    // Opzionale: puoi aggiungere metodi per altri colori
    public void ImpostaColore(Color nuovoColore)
    {
        materiale.color = nuovoColore;
    }
}