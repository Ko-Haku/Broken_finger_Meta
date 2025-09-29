using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class CambioScena : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) LoadSceneAsync(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) LoadSceneAsync(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) LoadSceneAsync(3);
        if (Input.GetKeyDown(KeyCode.Alpha4)) LoadSceneAsync(4);
        if (Input.GetKeyDown(KeyCode.Alpha5)) LoadSceneAsync(5);
        if (Input.GetKeyDown(KeyCode.Alpha6)) LoadSceneAsync(6);
       
       
    }

   


    void LoadSceneAsync(int index)
    {
        if (index >= 0 && index < SceneManager.sceneCountInBuildSettings)
        {
            // 🔐 Salva il nome della scena corrente come "scenaPrecedente"
            string scenaCorrente = SceneManager.GetActiveScene().name;
            CondizioneStato.scenaPrecedente = scenaCorrente;

            Debug.Log($"📦 Caricamento scena: {index} (da: {scenaCorrente})");
            SceneManager.LoadSceneAsync(index);
        }
        else
        {
            Debug.LogWarning("❌ Indice scena non valido: " + index);
        }
    }
}