using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

// Lato editor dei tasti di TB_TastiVideo: registra la Game view (quello che vede il partecipante,
// specchiato dal visore) in MP4 H.264 con Unity Recorder, in tempo reale (frequenza variabile).
// I file vanno in <progetto>/Video_Stimoli/<avatar>_<data_ora>.mp4 (fuori da Assets: niente import).
[InitializeOnLoad]
public static class TB_RegistraVideo
{
    const int Larghezza = 1920, Altezza = 1080;
    const float Fps = 30f;
    static RecorderController controller;
    static string fileCorrente;

    static TB_RegistraVideo()
    {
        TB_TastiVideo.Inizia += Inizia;
        TB_TastiVideo.Ferma += Ferma;
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingPlayMode) Ferma(); };
    }

    static void Inizia(string nome)
    {
        if (controller != null && controller.IsRecording()) return;
        string cartella = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Video_Stimoli");
        Directory.CreateDirectory(cartella);
        fileCorrente = Path.Combine(cartella, $"{nome}_{DateTime.Now:yyyyMMdd_HHmmss}");

        var impostazioni = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        var film = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        film.name = "Stimolo fMRI";
        film.Enabled = true;
        film.EncoderSettings = new CoreEncoderSettings
        {
            Codec = CoreEncoderSettings.OutputCodec.MP4,
            EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
        };
        film.ImageInputSettings = new GameViewInputSettings { OutputWidth = Larghezza, OutputHeight = Altezza };
        film.AudioInputSettings.PreserveAudio = false;
        film.OutputFile = fileCorrente;                 // l'estensione .mp4 la aggiunge il Recorder
        impostazioni.AddRecorderSettings(film);
        impostazioni.SetRecordModeToManual();
        // frequenza variabile: si registra in tempo reale. Con "Constant" il Recorder forza il tempo di gioco
        // a 1/Fps per fotogramma e, con il visore a 72-90 Hz, fisica e animazioni andrebbero più veloci del reale.
        impostazioni.FrameRate = Fps;
        impostazioni.FrameRatePlayback = FrameRatePlayback.Variable;
        impostazioni.CapFrameRate = false;

        controller = new RecorderController(impostazioni);
        controller.PrepareRecording();
        controller.StartRecording();
        TB_TastiVideo.StaRegistrando = true;
        Debug.Log($"[TB] Registrazione video iniziata ({Larghezza}x{Altezza}, tempo reale): F10 per fermare e salvare");
    }

    static void Ferma()
    {
        if (controller == null || !controller.IsRecording()) { TB_TastiVideo.StaRegistrando = false; return; }
        controller.StopRecording();
        controller = null;
        TB_TastiVideo.StaRegistrando = false;
        Debug.Log($"[TB] Video salvato: {fileCorrente}.mp4");
    }
}
