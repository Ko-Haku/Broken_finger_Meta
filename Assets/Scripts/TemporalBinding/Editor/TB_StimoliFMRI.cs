using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Scena per registrare i video stimoli fMRI: copia di TB_NuoviStrumenti in cui T alterna
//   AVATAR_Mano (mani maschili LM) -> AVATAR_Tentacolo -> AVATAR_GripperParallelo.
// I due tool vengono dal progetto Eseguito_Osservato (Assets/MODELS/EseguitoOsservato: prefab esportati
// dalla scena EEG_osservato_eseguito_TouchGrab, dove i supporti dei tool hanno la stessa posa della pinzetta
// rispetto all'osso della mano): qui si montano al posto della pinzetta, sul moncherino dell'avatar Tool,
// e si aprono col pinch (TB_AperturaTool, stessi parametri dell'originale).
// Rieseguibile: ricrea la scena da TB_NuoviStrumenti.
public static class TB_StimoliFMRI
{
    const string Sorgente = "Assets/Scenes/TemporalBinding/TB_NuoviStrumenti.unity";
    const string Scena = "Assets/Scenes/TemporalBinding/TB_StimoliFMRI.unity";
    const string Cartella = "Assets/MODELS/EseguitoOsservato/";

    static readonly (string prefab, string nome, string parametro, float apertoCm)[] Tool =
    {
        ("Tool_Tentacolo.prefab", "AVATAR_Tentacolo", "open", 10f),
        ("Tool_GripperParallelo.prefab", "AVATAR_GripperParallelo", "Blend", 12f),
    };

    [MenuItem("Tools/Temporal Binding/Scena stimoli fMRI (mano, tentacolo, gripper)")]
    public static void Esegui()
    {
        var log = new StringBuilder("\n===== TB_StimoliFMRI =====\n");
        if (EditorSceneManager.GetActiveScene().isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.DeleteAsset(Scena);
        AssetDatabase.CopyAsset(Sorgente, Scena);
        var scena = EditorSceneManager.OpenScene(Scena, OpenSceneMode.Single);

        var swap = Object.FindObjectsByType<TB_SwapEffettore>(FindObjectsInactive.Include, FindObjectsSortMode.None).First();
        GameObject TrovaAvatar(string n) => swap.avatar.FirstOrDefault(a => a && a.name == n);
        var mano = TrovaAvatar("AVATAR_Mano"); var tool = TrovaAvatar("AVATAR_Tool");
        if (!mano || !tool) { Debug.LogError("[TB] AVATAR_Mano o AVATAR_Tool mancanti in " + Sorgente); return; }

        var nuovi = new System.Collections.Generic.List<GameObject> { mano };
        foreach (var (prefab, nome, parametro, apertoCm) in Tool)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Cartella + prefab);
            if (!src) { log.AppendLine("!! manca " + prefab); continue; }
            var av = Object.Instantiate(tool, tool.transform.parent);
            av.name = nome; av.SetActive(false);

            // la pinzetta (maingripper sotto Pinzetta_Originale) lascia il posto al tool, con la stessa posa
            var pinzetta = av.GetComponentsInChildren<Transform>(true).First(t => t.name == "maingripper" && t.parent && t.parent.name == "Pinzetta_Originale");
            var t2 = ((GameObject)PrefabUtility.InstantiatePrefab(src, pinzetta.parent)).transform;
            t2.localPosition = pinzetta.localPosition; t2.localRotation = pinzetta.localRotation; t2.localScale = pinzetta.localScale;
            pinzetta.gameObject.SetActive(false);
            // in Nuovi Strumenti il moncherino porta l'artiglio steampunk e il supporto della pinzetta è spento
            pinzetta.parent.gameObject.SetActive(true);
            foreach (var art in av.GetComponentsInChildren<TB_ArtiglioPinch>(true)) art.gameObject.SetActive(false);
            SistemaVisibilita(t2);
            // materiale visibile dai due lati: alcune parti (calotte delle punte del tentacolo) hanno scala negativa
            // e piegandosi mostrano il retro, che col materiale a faccia singola spariva
            var dueFacce = MaterialeDueFacce();
            foreach (var r in t2.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = r.sharedMaterials.Select(m => m && m.name == "protesi" ? dueFacce : m).ToArray();

            // rigido con l'avambraccio come una protesi: stesso osso dell'anello (torsione dell'avambraccio),
            // così piegando il polso il tool non si stacca dall'anello
            var anello = av.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Anello_Polso");
            if (anello) t2.SetParent(anello.parent, true);

            // apertura col pinch della mano destra tracciata (quella che muove il moncherino)
            var mr = av.GetComponentsInChildren<TB_ManoRealistica>(true).First(m => m.polso && m.polso.name.Contains("Right"));
            var ap = t2.gameObject.AddComponent<TB_AperturaTool>();
            ap.hand = mr.hand; ap.animator = t2.GetComponentInChildren<Animator>(true); ap.parametro = parametro; ap.apertoCm = apertoCm;
            if (ap.animator) ap.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            nuovi.Add(av);
            log.AppendLine($"  + {nome}: {src.name} sul moncherino al posto della pinzetta, animator {(ap.animator ? ap.animator.runtimeAnimatorController.name : "-")} ({parametro}, 3-{apertoCm} cm)");
        }

        // via gli altri avatar: in questa scena T scorre solo mano, tentacolo, gripper
        foreach (var vecchio in swap.avatar.Where(a => a && !nuovi.Contains(a)).ToArray()) Object.DestroyImmediate(vecchio);
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!(mb is TB_SwapEffettore || mb is TB_PrimaPersona || mb is TB_CalibrazioneCorpo || mb is TB_Diagnostica)) continue;
            var so = new SerializedObject(mb);
            var lista = so.FindProperty("avatar");
            if (lista == null || !lista.isArray) continue;
            lista.ClearArray();
            foreach (var g in nuovi) { lista.InsertArrayElementAtIndex(lista.arraySize); lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = g; }
            var r = so.FindProperty("renderers");
            if (r != null && r.isArray) r.ClearArray();
            so.ApplyModifiedProperties();
        }
        swap.avatarMano = mano; swap.avatarTool = nuovi.Count > 1 ? nuovi[1] : null;
        EditorUtility.SetDirty(swap);
        for (int i = 0; i < nuovi.Count; i++) nuovi[i].SetActive(i == 0);

        // ---- tasti per i video (F9 inizia, F10 ferma e salva in <progetto>/Video_Stimoli)
        var tasti = new GameObject("TB_Video").AddComponent<TB_TastiVideo>();
        tasti.swap = swap;
        log.AppendLine("  + TB_Video: F9 inizia la registrazione, F10 ferma e salva l'MP4 in Video_Stimoli/");

        // ---- bicchiere giallo (Eseguito_Osservato) su un cilindro rosso, a sinistra del pulsante come nel video
        CilindroEBicchiere(log);

        // ---- raccordo tra anello e gripper, aspetto della scena per i video
        MontaRaccordo(log);
        ApplicaAspetto(log);
        AggiungiPresa(log);

        // ---- niente scritte segnaposto nei video ("New Text" rimasto nel Canvas di Nuovi Strumenti)
        foreach (var t in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.text == "New Text") { t.gameObject.SetActive(false); log.AppendLine($"  - spenta la scritta segnaposto {t.name}"); }

        EditorSceneManager.MarkSceneDirty(scena);
        EditorSceneManager.SaveScene(scena);
        log.AppendLine($"  T scorre: {string.Join(" > ", nuovi.Select(g => g.name))}");
        Debug.Log(log.Append("===== fine =====").ToString());
    }

    // Scelte di aspetto per i video (fatte a mano in scena dallo sperimentatore): lampadina, pulsante e oggetti
    // da prendere spenti, tavolo in plastica bianca.
    public static void ApplicaAspetto(StringBuilder log)
    {
        // TB_Lampadina sta su TB_Postazione (tavolo + pulsante + lampadina): si spegne solo il figlio "Lampadina"
        var lamp = Object.FindObjectsByType<TB_Lampadina>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (lamp)
        {
            lamp.gameObject.SetActive(true);
            foreach (var nome in new[] { "Lampadina", "Pulsante" })
            {
                var figlio = lamp.transform.Find(nome);
                if (figlio) figlio.gameObject.SetActive(false);
            }
        }
        var ogg = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "TB_OggettiDaPrendere");
        if (ogg) ogg.gameObject.SetActive(false);
        var bianco = AssetDatabase.LoadAssetAtPath<Material>("Assets/MODELS/ExampleAssets/Materials/Plastic_White_Mat.mat");
        var tavolo = GameObject.Find("Tavolo");
        if (tavolo && bianco) foreach (var r in tavolo.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = r.sharedMaterials.Select(_ => bianco).ToArray();
        log.AppendLine("  aspetto: lampadina, pulsante e oggetti da prendere spenti, tavolo Plastic_White_Mat");
    }

    // Raccordo metallico (Blender/costruisci_raccordo_gripper.py) tra Anello_Polso e la testa del gripper che
    // pende in basso: si allinea ai marcatori M_O/M_Z/M_Y sul sistema dell'avambraccio (origine sulla faccia
    // dell'anello verso la mano) e la testa del gripper si appoggia sotto la piastra (M_Base), in asse verticale.
    // Va fatto con l'avatar in posa T (in editor). Rieseguibile sulla scena aperta.
    public static void MontaRaccordo(StringBuilder log)
    {
        var swap = Object.FindObjectsByType<TB_SwapEffettore>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        var av = swap ? swap.avatar.FirstOrDefault(a => a && a.name == "AVATAR_GripperParallelo") : null;
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Cartella + "Raccordo_Gripper.fbx");
        if (!av || !fbx) { log.AppendLine("!! avatar gripper o Raccordo_Gripper.fbx mancanti"); return; }
        var T = av.GetComponentsInChildren<Transform>(true);
        var anello = T.First(t => t.name == "Anello_Polso"); var ar = anello.GetComponent<Renderer>();
        var ap = av.GetComponentInChildren<TB_AperturaTool>(true);
        Vector3 gomito = T.First(t => t.name == "mixamorig_RightForeArm").position, polso = T.First(t => t.name == "mixamorig_RightHand").position;
        Vector3 z = (polso - gomito).normalized, y = Vector3.ProjectOnPlane(Vector3.up, z).normalized;
        Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        Vector3 O = ar.bounds.center + z * Vector3.Dot(ar.bounds.extents, Abs(z));

        foreach (var vecchio in ap.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Raccordo_Gripper").ToArray())
            Object.DestroyImmediate(vecchio.gameObject);
        var r = ((GameObject)PrefabUtility.InstantiatePrefab(fbx, ap.transform)).transform;
        r.name = "Raccordo_Gripper";
        Transform M(string n) => r.GetComponentsInChildren<Transform>(true).First(t => t.name == n);
        // scala reale (1:1 in metri) anche sotto genitori scalati
        r.localScale = Vector3.one;
        var ls = r.lossyScale; r.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
        // rotazione: (M_Z - M_O, M_Y - M_O) -> (z, y), poi origine sulla faccia dell'anello
        var attuale = Quaternion.LookRotation(M("M_Z").position - M("M_O").position, M("M_Y").position - M("M_O").position);
        r.rotation = Quaternion.LookRotation(z, y) * Quaternion.Inverse(attuale) * r.rotation;
        r.position += O - M("M_O").position;

        // testa del gripper sotto la piastra, con le ganasce verso il basso
        var testa = ap.animator.transform; var corpo = ap.animator.GetComponent<Renderer>();
        var ganasce = ap.animator.GetComponentsInChildren<Renderer>(true).Where(x => x != corpo && x.enabled).ToArray();
        Vector3 asse = (ganasce.Aggregate(Vector3.zero, (s, x) => s + x.bounds.center) / ganasce.Length - corpo.bounds.center).normalized;
        float semi = Vector3.Dot(corpo.bounds.extents, Abs(asse));
        Vector3 base_ = corpo.bounds.center - asse * semi;
        var rot = Quaternion.FromToRotation(asse, -y);
        testa.SetPositionAndRotation(base_ + rot * (testa.position - base_), rot * testa.rotation);
        corpo = testa.GetComponent<Renderer>();
        base_ = corpo.bounds.center + y * Vector3.Dot(corpo.bounds.extents, Abs(y));
        testa.position += M("M_Base").position - base_;
        foreach (var x in r.GetComponentsInChildren<Renderer>(true)) x.enabled = true;
        log.AppendLine($"  + raccordo anello-gripper: testa ruotata di {Vector3.Angle(asse, -y):F0} gradi in verticale e appoggiata sotto la piastra");
    }

    // Aggiorna la scena salvata senza rigenerarla (conserva le modifiche fatte a mano):
    // polpastrelli visibili, raccordo ricostruito, aspetto, diagnostica delle ganasce. Anche da batch:
    // Unity.exe -batchmode -quit -projectPath ... -executeMethod TB_StimoliFMRI.AggiornaScena
    [MenuItem("Tools/Temporal Binding/Aggiorna scena stimoli fMRI (senza rigenerarla)")]
    public static void AggiornaScena()
    {
        var log = new StringBuilder("\n===== TB_StimoliFMRI.AggiornaScena =====\n");
        // se è già aperta si lavora su quella (con le eventuali modifiche non salvate), altrimenti la si apre
        var scena = EditorSceneManager.GetActiveScene();
        if (scena.path != Scena) scena = EditorSceneManager.OpenScene(Scena, OpenSceneMode.Single);
        foreach (var ap in Object.FindObjectsByType<TB_AperturaTool>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SistemaVisibilita(ap.transform);
        log.AppendLine("  tool sul layer Default, sfere indice/pollice e anello della radice spenti");
        MontaRaccordo(log);
        ApplicaAspetto(log);
        DiagnosticaGanasce(log);
        EditorSceneManager.MarkSceneDirty(scena);
        EditorSceneManager.SaveScene(scena);
        Debug.Log(log.Append("===== fine =====").ToString());
    }

    // Presa del bicchiere per gli stimoli (TB_PresaStimolo): sul bicchiere, con un effettore per avatar.
    // Il bicchiere non è più un oggetto fisico afferrabile (TB_Afferrabile/TB_Presa): lo muove solo la presa.
    public static void AggiungiPresa(StringBuilder log)
    {
        var cup = GameObject.Find("Bicchiere_Giallo");
        var swap = Object.FindObjectsByType<TB_SwapEffettore>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (!cup || !swap) { log.AppendLine("!! bicchiere o swap mancanti: niente presa"); return; }
        foreach (var a in cup.GetComponentsInChildren<TB_Afferrabile>(true)) Object.DestroyImmediate(a);
        foreach (var rb in cup.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }
        var presa = cup.GetComponent<TB_PresaStimolo>() ?? cup.AddComponent<TB_PresaStimolo>();
        presa.cilindro = GameObject.Find("Cilindro_Rosso")?.transform;
        presa.tavolo = GameObject.Find("Tavolo")?.transform;
        presa.effettori.Clear();
        foreach (var av in swap.avatar.Where(a => a))
        {
            var mr = av.GetComponentsInChildren<TB_ManoRealistica>(true).FirstOrDefault(m => m.polso && m.polso.name.Contains("Right"));
            var tool = av.GetComponentInChildren<TB_AperturaTool>(true);
            var e = new TB_PresaStimolo.Effettore { nome = av.name, avatar = av, hand = mr ? mr.hand : null, tool = tool };
            if (tool) e.ancora = tool.transform;
            else
            {
                var mano = av.GetComponentsInChildren<Transform>(true).First(t => t.name == "mixamorig_RightHand");
                e.ancora = mano;
                e.dita = mano.GetComponentsInChildren<Transform>(true)
                             .Where(t => t != mano && t.name.StartsWith("mixamorig_RightHand") && !t.name.EndsWith("_end")).ToArray();
            }
            presa.effettori.Add(e);
            log.AppendLine($"  presa: {av.name} ancora {e.ancora.name}" + (tool ? " (tool)" : $" ({e.dita.Length} ossa delle dita)"));
        }
        EditorUtility.SetDirty(presa);
    }

    // Nel progetto originale alcune parti (punte del tentacolo, ganasce del gripper) erano sul layer 6, che la
    // telecamera di questo progetto non disegna: tutto il tool va sul layer Default. Spente le sfere
    // "indice"/"pollice" (indicatori di contatto gialli dell'originale) e l'anello della radice
    // (mesh "maingripper": l'avatar ha già il suo Anello_Polso).
    static void SistemaVisibilita(Transform radice)
    {
        foreach (var t in radice.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        foreach (var r in radice.GetComponentsInChildren<Renderer>(true))
            r.enabled = r.transform != radice && r.name != "indice" && r.name != "pollice";
    }

    // Dove stanno le ganasce del gripper all'inizio e alla fine della clip (sotto la testa o dentro?)
    static void DiagnosticaGanasce(StringBuilder log)
    {
        var ap = Object.FindObjectsByType<TB_AperturaTool>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(a => a.parametro == "Blend");
        if (!ap || !ap.animator) return;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Cartella + "PinzaClose.anim");
        var corpo = ap.animator.GetComponent<Renderer>();
        var giu = -ap.animator.transform.root.up;
        foreach (float t in new[] { 0f, clip.length })
        {
            clip.SampleAnimation(ap.animator.gameObject, t);
            var c = corpo.bounds;
            foreach (Transform j in ap.animator.transform)
            {
                var r = j.GetComponent<Renderer>(); if (!r) continue;
                float sotto = Vector3.Dot(r.bounds.center - c.center, Vector3.down);
                float punta = Vector3.Dot(r.bounds.min - c.min, Vector3.down);
                log.AppendLine($"  PinzaClose t={t:F1}: {j.name} centro {sotto * 100:F1} cm sotto il centro della testa, distanza tra ganasce {(ap.animator.transform.Cast<Transform>().Select(x => x.GetComponent<Renderer>()).Where(x => x).Select(x => x.bounds.center).Aggregate((a, b) => a - b)).magnitude * 100:F1} cm");
            }
        }
        clip.SampleAnimation(ap.animator.gameObject, 0f);
    }

    static Material MaterialeDueFacce()
    {
        const string path = Cartella + "protesi_due_facce.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/MODELS/Materials/protesi.mat")) { name = "protesi_due_facce" };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetFloat("_Cull", 0f);              // URP Lit: Render Face = Both
        m.doubleSidedGI = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    const float DiametroCilindro = 0.10f, AltezzaCilindro = 0.05f;

    static void CilindroEBicchiere(StringBuilder log)
    {
        var lamp = Object.FindObjectsByType<TB_Lampadina>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        var tavolo = GameObject.Find("Tavolo");
        if (!lamp || !lamp.tasto || !tavolo) { log.AppendLine("!! pulsante o tavolo non trovati: niente bicchiere"); return; }
        var bt = new Bounds(tavolo.transform.position, Vector3.zero);
        foreach (var r in tavolo.GetComponentsInChildren<Renderer>()) bt.Encapsulate(r.bounds);
        float yTavolo = bt.max.y;
        // a sinistra del pulsante (dal punto di vista del partecipante, che guarda verso +Z)
        Vector3 base_ = new Vector3(lamp.tasto.position.x - 0.22f, yTavolo, lamp.tasto.position.z + 0.01f);

        var radice = new GameObject("TB_BicchiereSuCilindro").transform;
        var cil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cil.name = "Cilindro_Rosso"; cil.transform.SetParent(radice, false);
        cil.transform.position = base_ + Vector3.up * (AltezzaCilindro * 0.5f);
        cil.transform.localScale = new Vector3(DiametroCilindro, AltezzaCilindro * 0.5f, DiametroCilindro);   // il cilindro primitivo è alto 2
        Object.DestroyImmediate(cil.GetComponent<Collider>());
        var mc = cil.AddComponent<MeshCollider>(); mc.convex = true;
        var rosso = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TemporalBinding/TB_Oggetto_Rosso.mat");
        if (rosso) cil.GetComponent<Renderer>().sharedMaterial = rosso;

        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Cartella + "Bicchiere_giallo 1.fbx");
        if (!fbx) { log.AppendLine("!! Bicchiere_giallo 1.fbx mancante"); return; }
        var cup = (GameObject)PrefabUtility.InstantiatePrefab(fbx, radice);
        cup.name = "Bicchiere_Giallo";
        var giallo = AssetDatabase.LoadAssetAtPath<Material>("Assets/House_Furniture_Pack/Materials 1/yellow.mat");
        if (giallo) foreach (var r in cup.GetComponentsInChildren<Renderer>()) r.sharedMaterial = giallo;
        // appoggiato al centro della faccia superiore del cilindro
        var bc = cup.GetComponentInChildren<Renderer>().bounds;
        float yCima = base_.y + AltezzaCilindro;
        cup.transform.position += new Vector3(base_.x - bc.center.x, yCima + 0.001f - bc.min.y, base_.z - bc.center.z);
        var mcc = cup.GetComponentInChildren<MeshFilter>().gameObject.AddComponent<MeshCollider>(); mcc.convex = true;
        var rb = cup.AddComponent<Rigidbody>();
        rb.mass = 0.05f; rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        cup.AddComponent<TB_Afferrabile>();
        log.AppendLine($"  + cilindro rosso Ø{DiametroCilindro * 100:F0} x {AltezzaCilindro * 100:F0} cm e bicchiere giallo {bc.size.x * 100:F1} x {bc.size.y * 100:F1} cm a {base_:F3} (R lo rimette a posto)");
    }
}
