using System;
using UnityEngine;

// Tasti per registrare i video stimoli in Play (editor, Quest Link): F9 inizia, F10 ferma e salva.
// La registrazione vera la fa Unity Recorder dal lato editor (Editor/TB_RegistraVideo.cs), che ascolta
// questi eventi: le API del Recorder esistono solo nell'editor, non in questo assembly.
public class TB_TastiVideo : MonoBehaviour
{
    public KeyCode inizia = KeyCode.F9;
    public KeyCode ferma = KeyCode.F10;
    [Tooltip("Per il nome del file: l'avatar attivo (mano, tentacolo, gripper)")]
    public TB_SwapEffettore swap;

    public static event Action<string> Inizia;
    public static event Action Ferma;
    public static bool StaRegistrando;          // lo aggiorna il lato editor

    void Update()
    {
        if (Input.GetKeyDown(inizia) && !StaRegistrando) Inizia?.Invoke(NomeAvatar());
        else if (Input.GetKeyDown(ferma) && StaRegistrando) Ferma?.Invoke();
    }

    string NomeAvatar()
    {
        if (swap && swap.avatar != null)
            foreach (var a in swap.avatar)
                if (a && a.activeInHierarchy) return a.name.Replace("AVATAR_", "");
        return "clip";
    }

    void OnDisable() { if (StaRegistrando) Ferma?.Invoke(); }
}
