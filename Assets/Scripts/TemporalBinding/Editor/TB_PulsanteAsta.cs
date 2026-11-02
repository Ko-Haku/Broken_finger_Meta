using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Sostituisce il pulsante da tavolo con il pulsante su asta (Blender/costruisci_pulsante_asta_tb.py)
// nella scena pilota e ricollega tutto: tasto della lampadina, clicker del sensore, pistone
// dell'Esterno (montato sulla testa dell'asta), punte della pinzetta per il Tool, regolazione (C).
// Rieseguibile: se l'asta c'è già ne conserva posizione e altezza.
public static class TB_PulsanteAsta
{
    const string Scena = "Assets/Scenes/TemporalBinding/TemporalBinding_Pilota.unity";
    const string FbxPulsante = "Assets/MODELS/TemporalBinding/Postazione_TB/Pulsante_Asta.fbx";
    const string FbxPistone = "Assets/MODELS/TemporalBinding/Postazione_TB/Pistone_Asta.fbx";

    // posizione di default del tasto nella postazione: davanti al gomito appoggiato, a destra
    static readonly Vector3 TastoDefault = new Vector3(0.14f, 0f, 0.38f);   // y ignorata
    const float AltezzaDefault = 0.22f;                                     // piattello sopra il tavolo
    const float Corsa = 0.005f;

    [MenuItem("Tools/Temporal Binding/Pulsante su asta nella scena pilota")]
    public static void Esegui()
    {
        var scena = EditorSceneManager.GetActiveScene();
        if (scena.path != Scena) scena = EditorSceneManager.OpenScene(Scena, OpenSceneMode.Single);
        var log = new StringBuilder("\n===== TB_PulsanteAsta =====\n");
        Applica(log);
        EditorSceneManager.MarkSceneDirty(scena);
        EditorSceneManager.SaveScene(scena);
        Debug.Log(log.Append("===== fine =====").ToString());
    }

    public static TB_Pistone Applica(StringBuilder log)
    {
        var lamp = Trova<TB_Lampadina>();
        var man = Trova<TB_ExperimentManager>();
        var sensore = Trova<TB_PinchSensor>();
        if (!lamp) { Debug.LogError("[TB] TB_Lampadina non trovata"); return null; }
        Transform postazione = lamp.transform;
        var tavolo = postazione.Find("Tavolo");
        float yTavolo = tavolo ? tavolo.GetComponentInChildren<Renderer>().bounds.max.y : postazione.position.y;

        // direzione di sguardo orizzontale: dall'occhio alla lampadina
        var occhio = GameObject.Find("CenterEyeAnchor")?.transform;
        Vector3 d = (lamp.luce ? lamp.luce.transform.position : postazione.position + Vector3.forward) - (occhio ? occhio.position : Vector3.zero);
        d.y = 0; d = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;

        // ---- vecchio pulsante da tavolo e vecchio pistone: via (l'audio del clic si conserva)
        AudioSource vecchioClick = lamp.suonoClick;
        var esistente = postazione.Find("Pulsante_Asta");
        Vector3? posPrecedente = esistente ? esistente.position : (Vector3?)null;
        float? altPrecedente = null;
        if (esistente)
        {
            var t = Cerca(esistente, "Testa");
            if (t) altPrecedente = t.position.y - esistente.position.y;
        }

        var nuovo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FbxPulsante), postazione);
        nuovo.name = "Pulsante_Asta_nuovo";
        var click = nuovo.AddComponent<AudioSource>();
        if (vecchioClick) EditorUtility.CopySerialized(vecchioClick, click);
        else { click.playOnAwake = false; click.spatialBlend = 1f; }

        foreach (var nome in new[] { "Pulsante", "Pistone", "Pulsante_Asta" })
        {
            var v = postazione.Find(nome);
            if (v) { Object.DestroyImmediate(v.gameObject); log.AppendLine($"  - rimosso {nome}"); }
        }
        nuovo.name = "Pulsante_Asta";
        var root = nuovo.transform;
        var testa = Cerca(root, "Testa");
        var tasto = Cerca(root, "Tasto");

        // ---- orientamento: il FBX arriva con la Z di Blender in alto; il braccio deve puntare verso il partecipante
        var raddrizza = Quaternion.Euler(-90f, 0f, 0f);
        root.rotation = raddrizza;
        Vector3 off = tasto.position - root.position; off.y = 0;
        root.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(off, -d, Vector3.up), Vector3.up) * raddrizza;

        // ---- posizione: base sul tavolo, tasto nel punto di default (o dove era stato calibrato)
        if (posPrecedente.HasValue) root.position = posPrecedente.Value;
        else
        {
            root.position = new Vector3(0, yTavolo, 0);
            Vector3 target = postazione.TransformPoint(TastoDefault);
            off = tasto.position - root.position;
            root.position = new Vector3(target.x - off.x, yTavolo, target.z - off.z);
        }
        float h = altPrecedente ?? AltezzaDefault;
        testa.position = new Vector3(testa.position.x, root.position.y + h, testa.position.z);
        log.AppendLine($"  + Pulsante_Asta: base {root.position:F3}, tasto {tasto.position:F3}, piattello {h * 100:F1} cm sopra il tavolo");

        // ---- lampadina e sensore
        lamp.tasto = tasto;
        lamp.corsaTasto = Corsa;
        lamp.suonoClick = click;
        lamp.sensore = sensore;
        EditorUtility.SetDirty(lamp);
        if (sensore)
        {
            sensore.clicker = tasto;
            CollegaPunteTool(sensore, log);
            EditorUtility.SetDirty(sensore);
        }

        var reg = nuovo.AddComponent<TB_SupportoRegolabile>();
        reg.sensore = sensore; reg.testa = testa; reg.tasto = tasto;
        reg.ostacolo = postazione.Find("Lampadina");

        // ---- pistone sulla testa
        var pGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FbxPistone), testa);
        pGo.name = "Pistone";
        pGo.SetActive(true);
        pGo.transform.SetPositionAndRotation(testa.position, root.rotation);
        var asta = Cerca(pGo.transform, "Asta");
        var punta = Cerca(pGo.transform, "Punta");
        float yPunta = punta.GetComponent<Renderer>().bounds.min.y;
        float yTasto = tasto.GetComponent<Renderer>().bounds.max.y;
        Vector3 scarto = asta.position - tasto.position; scarto.y = 0;
        var p = pGo.AddComponent<TB_Pistone>();
        p.asta = asta;
        p.corsaContatto = yPunta - yTasto;
        p.corsaExtra = Corsa;
        pGo.SetActive(false);                    // visibile solo nei blocchi Esterno
        log.AppendLine($"  + Pistone sulla testa: corsa al contatto {p.corsaContatto * 1000:F0} mm, asta fuori asse {scarto.magnitude * 1000:F1} mm");
        if (man) { man.pistone = p; EditorUtility.SetDirty(man); }

        // ---- mirino: a metà tra tasto e lampadina
        var mirino = man ? man.mirino : null;
        if (mirino && lamp.luce)
        {
            Vector3 m = (tasto.position + lamp.luce.transform.position) * 0.5f;
            mirino.transform.position = m;
            log.AppendLine($"  mirino spostato a {m:F3}");
        }

        // ---- calibrazione dell'altezza del tavolo (V): sposta tutta la postazione + mirino
        var ct = postazione.GetComponent<TB_CalibraTavolo>() ?? postazione.gameObject.AddComponent<TB_CalibraTavolo>();
        ct.hand = sensore ? sensore.hand : null;
        ct.pianoTavolo = tavolo;
        ct.seguono = mirino ? new[] { mirino.transform } : new Transform[0];
        EditorUtility.SetDirty(ct);
        log.AppendLine($"  TB_CalibraTavolo su {postazione.name} (V con la mano piatta sul tavolo reale)");

        // ---- lunghezza del braccio (G con il gomito appoggiato)
        var cb = postazione.GetComponent<TB_CalibraBraccio>() ?? postazione.gameObject.AddComponent<TB_CalibraBraccio>();
        cb.pianoTavolo = tavolo;
        EditorUtility.SetDirty(cb);
        log.AppendLine("  TB_CalibraBraccio (G con il gomito sul tavolo e la mano in posa)");

        // ---- collisione della pinzetta con il pulsante
        if (sensore)
        {
            sensore.corsaTasto = Corsa;
            foreach (var drv in Object.FindObjectsByType<PinchOpenDriver_Interaction>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!drv.gripperAnimator || drv.transform.root == null || sensore.puntaPinzaA == null || !sensore.puntaPinzaA.IsChildOf(drv.transform)) continue;
                var ps = drv.GetComponent<TB_PinzaSulTasto>() ?? drv.gameObject.AddComponent<TB_PinzaSulTasto>();
                ps.sensore = sensore;
                ps.ganasciaSup = Cerca(drv.transform, "Bone.002");
                ps.ganasciaInf = Cerca(drv.transform, "Bone.001");
                ps.puntaSup = Cerca(drv.transform, "Bone.002_end");
                ps.puntaInf = Cerca(drv.transform, "Bone.001_end");
                EditorUtility.SetDirty(ps);
                log.AppendLine($"  TB_PinzaSulTasto su {drv.name}: ganasce speculari, le punte si fermano sul pulsante");
            }
        }
        return p;
    }

    // punti di presa visibili: punte di pollice e indice dell'avatar Mano (scalato rispetto alla mano reale)
    // e sfere in fondo alle ossa delle due ganasce della pinzetta
    static void CollegaPunteTool(TB_PinchSensor sensore, StringBuilder log)
    {
        var swap = Trova<TB_SwapEffettore>();
        GameObject mano = swap ? (swap.avatar != null && swap.avatar.Length > 0 ? swap.avatar[0] : swap.avatarMano) : null;
        if (mano)
        {
            sensore.puntaManoA = Cerca(mano.transform, "mixamorig_RightHandThumb4_end");
            sensore.puntaManoB = Cerca(mano.transform, "mixamorig_RightHandIndex4_end");
            log.AppendLine(sensore.puntaManoA && sensore.puntaManoB ? "  sensore: punte pollice/indice dell'avatar Mano" : "  !! punte delle dita dell'avatar Mano non trovate");
        }
        GameObject tool = swap ? (swap.avatar != null && swap.avatar.Length > 1 ? swap.avatar[1] : swap.avatarTool) : null;
        if (!tool) { log.AppendLine("  !! avatar Tool non trovato: punte della pinzetta non collegate"); return; }
        var driver = tool.GetComponentsInChildren<PinchOpenDriver_Interaction>(true).FirstOrDefault();
        Transform radice = driver ? driver.transform : tool.transform;
        var a = Cerca(radice, "Bone.001_end");
        var b = Cerca(radice, "Bone.002_end");
        sensore.puntaPinzaA = a; sensore.puntaPinzaB = b;
        log.AppendLine(a && b ? $"  sensore: punte pinzetta {a.name} / {b.name} (distanza {Vector3.Distance(a.position, b.position) * 1000:F0} mm)"
                              : "  !! punte della pinzetta non trovate");
    }

    static T Trova<T>() where T : Object =>
        Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();

    static Transform Cerca(Transform t, string nome)
    {
        if (t.name == nome) return t;
        foreach (Transform c in t) { var r = Cerca(c, nome); if (r) return r; }
        return null;
    }
}
