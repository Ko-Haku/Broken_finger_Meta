using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
#endif

// Registra in Play Mode la gerarchia di questo GameObject (es. mano tracciata o mano+pinzetta)
// in una AnimationClip, da riprodurre poi sul fantoccio nel passivo.
// F5 = inizia, F6 = ferma e salva in Assets/Scripts/TemporalBinding/Registrazioni.
// Solo in editor (con Quest Link).
public class TB_RegistratoreClip : MonoBehaviour
{
    public string nome = "Mano";
    public KeyCode inizia = KeyCode.F5;
    public KeyCode ferma = KeyCode.F6;
    [Tooltip("Frequenza dei campioni salvati nella clip")]
    public float fps = 90f;

#if UNITY_EDITOR
    GameObjectRecorder recorder;
    public bool StaRegistrando => recorder != null;

    void Update()
    {
        if (Input.GetKeyDown(inizia) && recorder == null)
        {
            recorder = new GameObjectRecorder(gameObject);
            recorder.BindComponentsOfType<Transform>(gameObject, true);
            Debug.Log($"[TB] Registrazione '{nome}' iniziata");
        }
        else if (Input.GetKeyDown(ferma) && recorder != null)
        {
            Salva();
        }
    }

    void LateUpdate()
    {
        if (recorder != null) recorder.TakeSnapshot(Time.deltaTime);
    }

    void Salva()
    {
        const string dir = "Assets/Scripts/TemporalBinding/Registrazioni";
        Directory.CreateDirectory(dir);
        var clip = new AnimationClip { frameRate = fps };
        recorder.SaveToClip(clip, fps);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{nome}_{System.DateTime.Now:yyyyMMdd_HHmmss}.anim");
        AssetDatabase.CreateAsset(clip, path);
        AssetDatabase.SaveAssets();
        recorder = null;
        Debug.Log($"[TB] Clip salvata: {path} ({clip.length:F2} s)");
    }

    void OnDisable() { if (recorder != null) Salva(); }
#endif
}
