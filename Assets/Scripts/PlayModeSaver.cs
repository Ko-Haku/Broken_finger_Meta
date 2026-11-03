using System.Globalization;
using UnityEngine;

/// <summary>
/// Salva posizione/rotazione/scala di questo GameObject in PlayerPrefs
/// mentre sei in Play mode, e la ripristina automaticamente allo Start successivo.
///
/// Tasti (quando il GO è attivo):
///   F5 = Salva posizione corrente
///   F6 = Carica ultima posizione salvata (anche in Play)
///
/// La posizione salvata persiste tra sessioni Play.
/// Metti questo script su qualsiasi GO che vuoi calibrare in Play.
/// </summary>
public class PlayModeSaver : MonoBehaviour
{
    private static readonly CultureInfo INV = CultureInfo.InvariantCulture;
    [Header("Impostazioni")]
    [Tooltip("Se true, applica automaticamente la posizione salvata allo Start.")]
    public bool autoRestoreOnStart = true;

    [Tooltip("Tasto per salvare (default F5).")]
    public KeyCode saveKey = KeyCode.F5;

    [Tooltip("Tasto per caricare (default F6).")]
    public KeyCode loadKey = KeyCode.F6;

    private string Key => $"PlayModeSaver_{gameObject.name}";

    private void Start()
    {
        if (autoRestoreOnStart && PlayerPrefs.HasKey(Key))
        {
            Load();
            Debug.Log($"[PlayModeSaver] '{gameObject.name}' → posizione ripristinata da salvataggio.");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(saveKey))
        {
            Save();
        }

        if (Input.GetKeyDown(loadKey))
        {
            Load();
        }
    }

    private void Save()
    {
        var t = transform;
        // Usa '|' come separatore per evitare conflitti con la virgola decimale
        string data = string.Format(INV,
            "{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}",
            t.localPosition.x, t.localPosition.y, t.localPosition.z,
            t.localRotation.x, t.localRotation.y, t.localRotation.z, t.localRotation.w,
            t.localScale.x, t.localScale.y, t.localScale.z);
        PlayerPrefs.SetString(Key, data);
        PlayerPrefs.Save();
        Debug.Log($"[PlayModeSaver] '{gameObject.name}' SALVATO → pos={t.localPosition}, scale={t.localScale}");
    }

    private void Load()
    {
        if (!PlayerPrefs.HasKey(Key))
        {
            Debug.LogWarning($"[PlayModeSaver] Nessun salvataggio per '{gameObject.name}'.");
            return;
        }

        string[] p = PlayerPrefs.GetString(Key).Split('|');
        if (p.Length < 10) return;

        transform.localPosition = new Vector3(
            float.Parse(p[0], INV), float.Parse(p[1], INV), float.Parse(p[2], INV));
        transform.localRotation = new Quaternion(
            float.Parse(p[3], INV), float.Parse(p[4], INV), float.Parse(p[5], INV), float.Parse(p[6], INV));
        transform.localScale = new Vector3(
            float.Parse(p[7], INV), float.Parse(p[8], INV), float.Parse(p[9], INV));

        Debug.Log($"[PlayModeSaver] '{gameObject.name}' CARICATO → pos={transform.localPosition}");
    }
}

