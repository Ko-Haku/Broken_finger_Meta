using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
#endif

// Registra in Play Mode la gerarchia di questo GameObject (la spalla destra dell'avatar Mano o Tool)
// in una AnimationClip, da riprodurre poi con TB_Cinematica nella condizione passiva.
// F5 = inizia, F6 = ferma e salva in Assets/Scripts/TemporalBinding/Registrazioni.
// L'istante in cui il TB_PinchSensor rileva la chiusura sul pulsante viene salvato nella clip come
// evento "TB_Chiusura": il replay lo fa coincidere con l'istante programmato del primo evento.
// Gira dopo VRIK, retargeting e torsione, così registra la posa finale. Solo in editor (Quest Link).
[DefaultExecutionOrder(32400)]
public class TB_RegistratoreClip : MonoBehaviour
{
    public const string Cartella = "Assets/Scripts/TemporalBinding/Registrazioni";

    public string nome = "Mano";
    public TB_PinchSensor sensore;
    public KeyCode inizia = KeyCode.F5;
    public KeyCode ferma = KeyCode.F6;
    [Tooltip("Frequenza dei campioni salvati nella clip")]
    public float fps = 80f;

#if UNITY_EDITOR
    GameObjectRecorder recorder;
    float tRegistrazione, tChiusura;
    public bool StaRegistrando => recorder != null;

    void Update()
    {
        if (Input.GetKeyDown(inizia) && recorder == null)
        {
            recorder = new GameObjectRecorder(gameObject);
            recorder.BindComponentsOfType<Transform>(gameObject, true);
            tRegistrazione = 0f; tChiusura = -1f;
            Debug.Log($"[TB] Registrazione '{nome}' iniziata: apri pollice e indice sul pulsante, chiudi una volta, poi F6");
        }
        else if (Input.GetKeyDown(ferma) && recorder != null)
        {
            Salva();
        }
    }

    void LateUpdate()
    {
        if (recorder == null) return;
        recorder.TakeSnapshot(Time.deltaTime);
        tRegistrazione = recorder.currentTime;
        if (tChiusura < 0 && sensore && sensore.ChiusuraQuestoFrame)
        {
            tChiusura = tRegistrazione;
            Debug.Log($"[TB] Chiusura a {tChiusura:F2} s");
        }
    }

    void Salva()
    {
        Directory.CreateDirectory(Cartella);
        var clip = new AnimationClip { frameRate = fps };
        recorder.SaveToClip(clip, fps);
        recorder = null;
        if (tChiusura < 0)
        {
            Debug.LogWarning($"[TB] Registrazione '{nome}' scartata: nessuna chiusura sul pulsante rilevata");
            return;
        }
        AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = tChiusura, functionName = TB_Cinematica.EventoChiusura } });
        string path = AssetDatabase.GenerateUniqueAssetPath($"{Cartella}/{nome}_{System.DateTime.Now:yyyyMMdd_HHmmss}.anim");
        AssetDatabase.CreateAsset(clip, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[TB] Clip salvata: {path} ({clip.length:F2} s, chiusura a {tChiusura:F2} s)");
    }

    void OnDisable() { if (recorder != null) Salva(); }
#endif
}
