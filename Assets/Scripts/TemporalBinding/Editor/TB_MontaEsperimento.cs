using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Monta il paradigma nella scena pilota (rieseguibile: ricostruisce TB_Esperimento e il pistone).
//  - TB_Esperimento: manager, sensore del pinch, logger, slider, cinematiche Mano/Tool, ParticipantInfo
//  - registratori delle clip passive sulle spalle destre dei due avatar (F5/F6)
//  - interfaccia world-space (istruzioni + slider) dietro la lampadina, mirino tra pulsante e lampadina
//  - pistone della condizione Esterno con l'asta sopra il tasto
//  - profilo Bloom ricostruito (il componente era andato perso)
// Batch: Unity.exe -batchmode -quit -projectPath ... -executeMethod TB_MontaEsperimento.Esegui
public static class TB_MontaEsperimento
{
    const string Scena = "Assets/Scenes/TemporalBinding_Pilota.unity";
    const string FbxPistone = "Assets/MODELS/TemporalBinding/Postazione_TB/Pistone.fbx";
    const string ProfiloBloom = "Assets/MODELS/TemporalBinding/Postazione_TB/Bloom_TB.asset";

    static readonly StringBuilder Log = new StringBuilder();
    static void L(string s) => Log.AppendLine("  " + s);

    [MenuItem("Tools/Temporal Binding/Monta esperimento nella scena pilota")]
    public static void Esegui()
    {
        Log.Clear().AppendLine("\n===== TB_MontaEsperimento =====");
        var scena = EditorSceneManager.OpenScene(Scena, OpenSceneMode.Single);

        var lamp = Trova<TB_Lampadina>();
        var swap = Trova<TB_SwapEffettore>();
        var occhio = GameObject.Find("CenterEyeAnchor")?.transform;
        if (!lamp || !swap) { Debug.LogError("[TB] Mancano TB_Lampadina o TB_SwapEffettore nella scena"); return; }
        GameObject avMano = swap.avatar != null && swap.avatar.Length > 0 ? swap.avatar[0] : swap.avatarMano;
        GameObject avTool = swap.avatar != null && swap.avatar.Length > 1 ? swap.avatar[1] : swap.avatarTool;
        Transform tasto = lamp.tasto;
        Transform postazione = lamp.transform;

        // direzione di sguardo orizzontale: dall'occhio alla lampadina
        Vector3 posOcchio = occhio ? occhio.position : Vector3.zero;
        Vector3 d = lamp.luce ? lamp.luce.transform.position - posOcchio : Vector3.forward;
        d.y = 0; d = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
        L($"occhio {posOcchio}, direzione {d}");

        // ---- mano destra tracciata (stessa sorgente del retargeting dell'avatar Mano)
        var manoDx = avMano.GetComponentsInChildren<TB_ManoRealistica>(true)
                           .FirstOrDefault(m => m.polso && m.polso.name.Contains("Right"));
        if (!manoDx || !manoDx.hand) { Debug.LogError("[TB] TB_ManoRealistica destra o la sua Hand non trovata"); return; }
        L($"mano destra: {manoDx.name} -> Hand '{manoDx.hand.name}'");

        // ---- TB_Esperimento (ricostruito da zero)
        var vecchio = GameObject.Find("TB_Esperimento");
        if (vecchio) Object.DestroyImmediate(vecchio);
        var root = new GameObject("TB_Esperimento");

        var sensore = root.AddComponent<TB_PinchSensor>();
        sensore.hand = manoDx.hand;
        sensore.clicker = tasto;
        var logger = root.AddComponent<TB_Logger>();
        var man = root.AddComponent<TB_ExperimentManager>();

        if (!Trova<ParticipantInfo>())
        {
            var pi = new GameObject("ParticipantInfo").AddComponent<ParticipantInfo>();
            pi.transform.SetParent(root.transform);
            L("+ ParticipantInfo (compila Codice_Soggetto e NumeroSoggetto prima di ogni sessione)");
        }

        man.sensore = sensore; man.lampadina = lamp; man.logger = logger; man.swap = swap;
        man.cinematicaMano = Cinematica(root, "Cinematica_Mano", avMano, "Mano_", sensore);
        man.cinematicaTool = Cinematica(root, "Cinematica_Tool", avTool, "Tool_", sensore);

        // ---- interfaccia
        CostruisciUI(root.transform, man, lamp, tasto, d);

        // ---- pistone
        man.pistone = CostruisciPistone(postazione, lamp, tasto, d);

        // ---- luce sempre abilitata, intensità gestita da TB_Lampadina
        if (lamp.luce)
        {
            if (lamp.luce.intensity > 0.01f) lamp.intensitaLuce = lamp.luce.intensity;
            lamp.luce.enabled = true;
            // raggio contenuto: le ombre della luce puntiforme ridisegnano solo ciò che sta nel raggio
            // (6 facce x 2 occhi), quindi il tavolo sì, l'ufficio no
            lamp.luce.range = 1.5f;
            L($"Luce_Filamento: intensità accesa {lamp.intensitaLuce}, range {lamp.luce.range} m, ombre {lamp.luce.shadows}");
            EditorUtility.SetDirty(lamp);
        }

        RiparaBloom();

        EditorSceneManager.MarkSceneDirty(scena);
        EditorSceneManager.SaveScene(scena);
        Log.AppendLine("===== fine =====");
        Debug.Log(Log.ToString());
    }

    static T Trova<T>() where T : Object =>
        Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();

    static Transform Cerca(Transform t, string nome)
    {
        if (t.name.Contains(nome)) return t;
        foreach (Transform c in t) { var r = Cerca(c, nome); if (r) return r; }
        return null;
    }

    static TB_Cinematica Cinematica(GameObject root, string nome, GameObject avatar, string prefisso, TB_PinchSensor sensore)
    {
        var spalla = Cerca(avatar.transform, "RightShoulder");
        var go = new GameObject(nome); go.transform.SetParent(root.transform);
        var cin = go.AddComponent<TB_Cinematica>();
        cin.fantoccio = spalla ? spalla.gameObject : null;
        cin.prefissoClip = prefisso;

        if (spalla)
        {
            foreach (var r in spalla.GetComponents<TB_RegistratoreClip>()) Object.DestroyImmediate(r);
            var reg = spalla.gameObject.AddComponent<TB_RegistratoreClip>();
            reg.nome = prefisso.TrimEnd('_');
            reg.sensore = sensore;
            L($"{nome}: radice {spalla.name} di {avatar.name}, registratore '{reg.nome}' (F5/F6)");
        }
        else L($"!! {nome}: spalla destra non trovata in {avatar.name}");
        return cin;
    }

    // ------------------------------------------------------------------ UI

    static void CostruisciUI(Transform root, TB_ExperimentManager man, TB_Lampadina lamp, Transform tasto, Vector3 d)
    {
        Vector3 pLamp = lamp.luce ? lamp.luce.transform.position : lamp.transform.position;
        var font = TMP_Settings.defaultFontAsset;

        // pannello principale: 30 cm oltre la lampadina, all'altezza degli occhi da seduti
        var canvasGo = new GameObject("TB_UI", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(root, false);
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)canvasGo.transform;
        rt.sizeDelta = new Vector2(700, 380);
        rt.localScale = Vector3.one * 0.001f;                 // 1 unità = 1 mm
        rt.position = new Vector3(pLamp.x, 1.12f, pLamp.z) + d * 0.35f;
        rt.rotation = Quaternion.LookRotation(d, Vector3.up);

        var istr = Testo(rt, "Istruzioni", font, 30, new Vector2(0, 80), new Vector2(680, 200));
        istr.text = "Premi X per iniziare";
        man.istruzioni = istr;

        // slider
        var pannello = new GameObject("Pannello_Slider", typeof(RectTransform));
        pannello.transform.SetParent(rt, false);
        var prt = (RectTransform)pannello.transform; prt.anchoredPosition = new Vector2(0, -90); prt.sizeDelta = new Vector2(600, 140);
        var risorse = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        };
        var sGo = DefaultControls.CreateSlider(risorse);
        sGo.transform.SetParent(prt, false);
        var srt = (RectTransform)sGo.transform; srt.anchoredPosition = new Vector2(0, -25); srt.sizeDelta = new Vector2(560, 30);
        var slider = sGo.GetComponent<Slider>(); slider.interactable = false;
        var cb = slider.colors; cb.disabledColor = Color.white; slider.colors = cb;
        var valore = Testo(prt, "Valore", font, 40, new Vector2(0, 35), new Vector2(400, 60));
        valore.text = "500 ms";
        var min = Testo(prt, "Min", font, 22, new Vector2(-280, -60), new Vector2(120, 30)); min.text = "1";
        var max = Testo(prt, "Max", font, 22, new Vector2(280, -60), new Vector2(120, 30)); max.text = "1000";

        var sr = root.gameObject.AddComponent<TB_SliderRisposta>();
        sr.pannello = pannello; sr.slider = slider; sr.valore = valore;
        man.slider = sr;

        // mirino: "+" sospeso a metà tra pulsante e lampadina, 10 cm sopra il tavolo
        var mGo = new GameObject("Mirino", typeof(Canvas));
        mGo.transform.SetParent(root, false);
        mGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var mrt = (RectTransform)mGo.transform;
        mrt.sizeDelta = new Vector2(60, 60); mrt.localScale = Vector3.one * 0.001f;
        Vector3 medio = (tasto.position + pLamp) * 0.5f;
        mrt.position = new Vector3(medio.x, tasto.position.y + 0.10f, medio.z);
        mrt.rotation = Quaternion.LookRotation(d, Vector3.up);
        var croce = Testo(mrt, "Croce", font, 60, Vector2.zero, new Vector2(60, 60));
        croce.text = "+"; croce.color = Color.white;
        man.mirino = mGo;
        mGo.SetActive(false);

        L($"UI: pannello a {rt.position}, mirino a {mrt.position}");
    }

    static TextMeshProUGUI Testo(RectTransform parent, string nome, TMP_FontAsset font, float size, Vector2 pos, Vector2 dim)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = Color.white;
        t.textWrappingMode = TextWrappingModes.Normal;
        var r = (RectTransform)go.transform; r.anchoredPosition = pos; r.sizeDelta = dim;
        return t;
    }

    // ------------------------------------------------------------------ pistone

    static TB_Pistone CostruisciPistone(Transform postazione, TB_Lampadina lamp, Transform tasto, Vector3 d)
    {
        var vecchio = postazione.Find("Pistone");
        if (vecchio) Object.DestroyImmediate(vecchio.gameObject);
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPistone);
        if (!fbx) { L("!! Pistone.fbx non trovato"); return null; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx, postazione);
        go.name = "Pistone";
        go.SetActive(true);
        var asta = Cerca(go.transform, "Asta");
        var punta = Cerca(go.transform, "Punta");

        // ruota: dalla colonna (origine) all'asta deve puntare verso il partecipante (-d)
        // il FBX arriva con la Z di Blender in alto: prima la si porta su Y
        var raddrizza = Quaternion.Euler(-90f, 0f, 0f);
        go.transform.rotation = raddrizza;
        Vector3 off = asta.position - go.transform.position; off.y = 0;
        go.transform.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(off, -d, Vector3.up), Vector3.up) * raddrizza;
        // base sul tavolo (stessa quota della base del pulsante), asta sopra il centro del tasto
        float yTavolo = tasto.parent.position.y;     // origine del pulsante = base sul tavolo
        off = asta.position - go.transform.position;
        go.transform.position = new Vector3(tasto.position.x - off.x, yTavolo, tasto.position.z - off.z);

        float yPunta = punta.GetComponent<Renderer>().bounds.min.y;
        float yTasto = tasto.GetComponentInChildren<Renderer>().bounds.max.y;
        var p = go.AddComponent<TB_Pistone>();
        p.asta = asta;
        p.corsaContatto = yPunta - yTasto;
        p.corsaExtra = lamp.corsaTasto;
        go.SetActive(false);                      // visibile solo nei blocchi Esterno
        L($"Pistone: base {go.transform.position}, corsa al contatto {p.corsaContatto * 1000:F0} mm");
        return p;
    }

    // ------------------------------------------------------------------ bloom

    static void RiparaBloom()
    {
        var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfiloBloom);
        if (!prof) { L("!! profilo bloom non trovato"); return; }
        prof.components.RemoveAll(c => c == null);
        if (!prof.TryGet<Bloom>(out var b))
        {
            b = prof.Add<Bloom>(true);
            b.name = "Bloom";
            AssetDatabase.AddObjectToAsset(b, prof);
        }
        b.threshold.Override(0.9f);
        b.intensity.Override(1.2f);
        b.scatter.Override(0.6f);
        EditorUtility.SetDirty(prof);
        AssetDatabase.SaveAssets();
        L("Bloom ricostruito nel profilo (soglia 0.9, intensità 1.2)");
    }
}
