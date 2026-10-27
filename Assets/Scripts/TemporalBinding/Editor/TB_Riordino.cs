using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Riordino del progetto (2026-10-27). Usa AssetDatabase.MoveAsset: i GUID restano, nessun riferimento si rompe.
// Cancella solo asset che nessuna scena/prefab usa (e il template URP). Tutto è recuperabile da git.
// Batch: Unity.exe -batchmode -quit -projectPath ... -executeMethod TB_Riordino.Esegui
public static class TB_Riordino
{
    static readonly StringBuilder Log = new StringBuilder();

    public static void Esegui()
    {
        Log.Clear().AppendLine("\n===== TB_Riordino =====");

        // --- scene raggruppate per esperimento
        Sposta("Assets/Scenes/TemporalBinding_Pilota.unity", "Assets/Scenes/TemporalBinding");
        Sposta("Assets/Scenes/TB_NuoviStrumenti.unity", "Assets/Scenes/TemporalBinding");
        Sposta("Assets/Scenes/MovimentoImpossibile.unity", "Assets/Scenes/MovimentoImpossibile");
        Sposta("Assets/Scenes/MovimentoImpossibile 1.unity", "Assets/Scenes/MovimentoImpossibile");
        Sposta("Assets/Scenes/MovimentoImpossibile 2.unity", "Assets/Scenes/MovimentoImpossibile");
        Sposta("Assets/Scenes/MovimentoImpossibileSettings.lighting", "Assets/Scenes/MovimentoImpossibile");
        Sposta("Assets/Scenes/EEG_osservato_eseguito.unity", "Assets/Scenes/EEG");
        Sposta("Assets/Scenes/HtcVive", "Assets/Scenes/Vecchie");

        // --- asset che non sono scene, fuori da Scenes
        Sposta("Assets/Scenes/AfroAmerican.mat", "Assets/Materials");
        Sposta("Assets/Scenes/Apri_pinza.anim", "Assets/Animazioni");
        Sposta("Assets/Scenes/apri_pinzetta.anim", "Assets/Animazioni");
        Sposta("Assets/Scenes/R_Wrist.controller", "Assets/Animazioni");
        Sposta("Assets/Scenes/pinze_dita (1).controller", "Assets/Animazioni");
        Sposta("Assets/Scenes/Hand_02.fbx", "Assets/MODELS");
        Sposta("Assets/Scenes/Attraversamento", "Assets/Textures");
        Unisci("Assets/Scenes/prefabs", "Assets/prefabs");
        Unisci("Assets/prefabs/prefabs", "Assets/prefabs");

        // --- file sciolti nella radice di Assets
        Sposta("Assets/InputSystem_Actions.inputactions", "Assets/Settings");
        Sposta("Assets/BLINDED_AM_ME", "Assets/Scripts");            // usato da KatanaCut.cs
        Sposta("Assets/_misuraprop", "Assets/prefabs");              // fpsmanager + materiali, usati da scene vecchie
        Sposta("Assets/TriggerStation_Plugins", "Assets/Plugins");   // dll dei trigger EEG

        // --- template URP e cartelle vuote
        Cancella("Assets/TutorialInfo");
        Cancella("Assets/Readme.asset");
        if (Directory.Exists("Assets/StreamingAssets") && !Directory.EnumerateFiles("Assets/StreamingAssets", "*", SearchOption.AllDirectories).Any(f => !f.EndsWith(".meta")))
            Cancella("Assets/StreamingAssets");

        // --- sample dei pacchetti Meta: via se nessuna scena/prefab del progetto li usa (si reimportano dal Package Manager)
        CancellaSeNonUsata("Assets/Samples");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        foreach (var c in new[] { "Assets/Scenes", "Assets/prefabs", "Assets/MODELS", "Assets/Animazioni", "Assets/Textures", "Assets/Materials" })
            RimuoviCartelleVuote(c);
        Log.AppendLine("===== fine =====");
        Debug.Log(Log.ToString());
    }

    static bool Esiste(string p) => AssetDatabase.IsValidFolder(p) || File.Exists(p);

    static void AssicuraCartella(string p)
    {
        if (AssetDatabase.IsValidFolder(p)) return;
        AssicuraCartella(Path.GetDirectoryName(p).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(p).Replace('\\', '/'), Path.GetFileName(p));
    }

    static void Sposta(string src, string cartella)
    {
        if (!Esiste(src)) { Log.AppendLine($"  (manca) {src}"); return; }
        AssicuraCartella(cartella);
        string dst = $"{cartella}/{Path.GetFileName(src)}";
        if (Esiste(dst))
        {
            if (AssetDatabase.IsValidFolder(src) && AssetDatabase.IsValidFolder(dst)) { Unisci(src, dst); return; }
            dst = AssetDatabase.GenerateUniqueAssetPath(dst);
        }
        string err = AssetDatabase.MoveAsset(src, dst);
        Log.AppendLine(string.IsNullOrEmpty(err) ? $"  {src} -> {dst}" : $"  !! {src}: {err}");
    }

    // sposta il contenuto di src dentro dst (cartelle unite ricorsivamente, file omonimi rinominati)
    static void Unisci(string src, string dst)
    {
        if (!AssetDatabase.IsValidFolder(src)) { Log.AppendLine($"  (manca) {src}"); return; }
        AssicuraCartella(dst);
        foreach (var figlio in AssetDatabase.GetSubFolders(src).Concat(
                     Directory.GetFiles(src).Where(f => !f.EndsWith(".meta")).Select(f => f.Replace('\\', '/'))).ToList())
            Sposta(figlio, dst);
        if (!Directory.EnumerateFileSystemEntries(src).Any(e => !e.EndsWith(".meta")))
            AssetDatabase.DeleteAsset(src);
    }

    static void Cancella(string p)
    {
        if (!Esiste(p)) return;
        Log.AppendLine(AssetDatabase.DeleteAsset(p) ? $"  cancellato {p}" : $"  !! non cancellato {p}");
    }

    static void CancellaSeNonUsata(string cartella)
    {
        if (!AssetDatabase.IsValidFolder(cartella)) return;
        var dentro = new HashSet<string>(AssetDatabase.FindAssets("", new[] { cartella }).Select(AssetDatabase.GUIDToAssetPath));
        var radici = AssetDatabase.FindAssets("t:Scene t:Prefab", new[] { "Assets" })
                                  .Select(AssetDatabase.GUIDToAssetPath)
                                  .Where(p => !p.StartsWith(cartella + "/")).ToArray();
        var usati = AssetDatabase.GetDependencies(radici, true).Where(dentro.Contains).ToList();
        if (usati.Count == 0) Cancella(cartella);
        else Log.AppendLine($"  {cartella} tenuta: {usati.Count} asset usati, es. {usati[0]}");
    }

    static void RimuoviCartelleVuote(string radice)
    {
        foreach (var d in Directory.GetDirectories(radice))
        {
            RimuoviCartelleVuote(d);
            if (!Directory.EnumerateFileSystemEntries(d).Any(e => !e.EndsWith(".meta")) && !Directory.EnumerateDirectories(d).Any())
            {
                string p = d.Replace('\\', '/');
                if (AssetDatabase.DeleteAsset(p)) Log.AppendLine($"  cartella vuota rimossa {p}");
            }
        }
    }
}
