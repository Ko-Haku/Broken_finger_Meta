using UnityEngine;

// Prova in Play: alterna gli avatar (es. mano realistica / moncherino + tool / braccio robotico).
// T da tastiera oppure Y sul controller sinistro. Con 'avatar' vuoto usa la coppia Mano/Tool.
public class TB_SwapEffettore : MonoBehaviour
{
    public GameObject avatarMano;
    public GameObject avatarTool;
    [Tooltip("Se compilato, T scorre questi avatar in ordine (ignora Mano/Tool)")]
    public GameObject[] avatar;
    public KeyCode tasto = KeyCode.T;
    public bool inizioConMano = true;

    public bool ManoAttiva => Indice == 0;
    public int Indice { get; private set; }

    GameObject[] Lista => avatar != null && avatar.Length > 0 ? avatar : new[] { avatarMano, avatarTool };

    void Start() { Mostra(inizioConMano ? 0 : 1); }

    void Update()
    {
        if (Input.GetKeyDown(tasto) || TB_Input.YGiu)
            Mostra((Indice + 1) % Lista.Length);
    }

    public void Imposta(bool mano) => Mostra(mano ? 0 : 1);

    public void Mostra(int i)
    {
        var l = Lista; Indice = i;
        for (int k = 0; k < l.Length; k++) if (l[k]) l[k].SetActive(k == i);
    }
}
