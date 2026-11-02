using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Mette sul tavolo di TB_NuoviStrumenti due oggetti da prendere (cubo e pallina), aggiunge
// TB_Presa a tutte le mani degli avatar (mano, artiglio steampunk, robot) e i collider delle dita
// (TB_DitaFisiche) a mano vera e robot, per spingere gli oggetti senza afferrarli. Rieseguibile.
// Batch: Unity.exe -batchmode -quit -projectPath ... -executeMethod TB_AggiungiOggetti.Esegui
public static class TB_AggiungiOggetti
{
    const string Scena = "Assets/Scenes/TemporalBinding/TB_NuoviStrumenti.unity";
    const string CartellaMateriali = "Assets/Materials/TemporalBinding";

    [MenuItem("Tools/Temporal Binding/Oggetti da prendere in Nuovi Strumenti")]
    public static void Esegui()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scena = EditorSceneManager.OpenScene(Scena, OpenSceneMode.Single);
        var log = new System.Text.StringBuilder("\n===== TB_AggiungiOggetti =====\n");

        // ---- presa su ogni mano di ogni avatar
        foreach (var m in Object.FindObjectsByType<TB_ManoRealistica>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var p = m.GetComponent<TB_Presa>();
            if (!p) p = m.gameObject.AddComponent<TB_Presa>();
            p.mano = m;
            Transform av = m.transform;
            while (av.parent && !av.name.StartsWith("AVATAR_")) av = av.parent;
            p.artiglio = av.GetComponentsInChildren<TB_ArtiglioPinch>(true).FirstOrDefault(a => a.hand == m.hand);
            p.tolleranza = p.artiglio ? 0.04f : 0.025f;
            EditorUtility.SetDirty(p);

            // collider sulle dita della mano vera e del robot (non sull'artiglio)
            var f = m.GetComponent<TB_DitaFisiche>();
            if (p.artiglio) { if (f) Object.DestroyImmediate(f); }
            else
            {
                if (!f) f = m.gameObject.AddComponent<TB_DitaFisiche>();
                f.mano = m;
                f.raggioDito = av.name.Contains("Robot") ? 0.011f : 0.009f;
                EditorUtility.SetDirty(f);
            }
            log.AppendLine($"  presa: {av.name}/{m.name} -> {(p.artiglio ? "artiglio" : "dita + collider")}");
        }

        // ---- posizione sul tavolo: accanto al pulsante, verso la mano destra
        var lamp = Object.FindObjectsByType<TB_Lampadina>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (!lamp || !lamp.tasto) { Debug.LogError("[TB] TB_Lampadina o il suo tasto non trovati"); return; }
        Transform tasto = lamp.tasto;
        float yTavolo = tasto.parent.position.y;          // origine del pulsante = base sul tavolo
        var occhio = GameObject.Find("CenterEyeAnchor")?.transform;
        Vector3 avanti = (lamp.luce ? lamp.luce.transform.position : tasto.position) - (occhio ? occhio.position : Vector3.zero);
        avanti.y = 0; avanti = avanti.sqrMagnitude > 1e-4f ? avanti.normalized : Vector3.forward;
        Vector3 destra = Vector3.Cross(Vector3.up, avanti);
        Vector3 basePos = new Vector3(tasto.position.x, yTavolo, tasto.position.z);

        // ---- il tavolo deve avere un collider, altrimenti gli oggetti cadono attraverso
        var tavolo = GameObject.Find("Tavolo");
        if (tavolo && !tavolo.GetComponentInChildren<Collider>())
        {
            var b = new Bounds(tavolo.transform.position, Vector3.zero);
            foreach (var r in tavolo.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            var piano = GameObject.Find("TB_PianoTavolo") ?? new GameObject("TB_PianoTavolo");
            piano.transform.SetParent(tavolo.transform.parent, true);
            piano.transform.SetPositionAndRotation(new Vector3(b.center.x, yTavolo - 0.01f, b.center.z), Quaternion.identity);
            piano.transform.localScale = Vector3.one;
            var bc = piano.GetComponent<BoxCollider>();
            if (!bc) bc = piano.AddComponent<BoxCollider>();
            bc.size = new Vector3(b.size.x, 0.02f, b.size.z);
            log.AppendLine($"  + piano di collisione del tavolo {bc.size.x:F2} x {bc.size.z:F2} m a quota {yTavolo:F3}");
        }
        else log.AppendLine(tavolo ? "  tavolo: collider già presente" : "  !! oggetto 'Tavolo' non trovato");

        // ---- oggetti (ricostruiti da zero)
        var vecchio = GameObject.Find("TB_OggettiDaPrendere");
        if (vecchio) Object.DestroyImmediate(vecchio);
        var radice = new GameObject("TB_OggettiDaPrendere").transform;

        const float lato = 0.045f, diametro = 0.05f;
        Crea(radice, PrimitiveType.Cube, "Cubo", lato, Materiale("TB_Oggetto_Legno", new Color(0.72f, 0.48f, 0.26f), 0.35f),
             basePos + destra * 0.15f + avanti * 0.02f + Vector3.up * (lato * 0.5f + 0.002f));
        Crea(radice, PrimitiveType.Sphere, "Pallina", diametro, Materiale("TB_Oggetto_Rosso", new Color(0.75f, 0.08f, 0.07f), 0.6f),
             basePos + destra * 0.24f - avanti * 0.03f + Vector3.up * (diametro * 0.5f + 0.002f));
        log.AppendLine($"  + Cubo {lato * 100:F1} cm e Pallina {diametro * 100:F0} cm a destra del pulsante (R le rimette a posto)");

        EditorSceneManager.MarkSceneDirty(scena);
        EditorSceneManager.SaveScene(scena);
        log.AppendLine("===== fine =====");
        Debug.Log(log.ToString());
    }

    static void Crea(Transform radice, PrimitiveType tipo, string nome, float misura, Material mat, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nome;
        go.transform.SetParent(radice, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * misura;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        go.AddComponent<TB_Afferrabile>();
    }

    static Material Materiale(string nome, Color colore, float lucidita)
    {
        if (!AssetDatabase.IsValidFolder(CartellaMateriali)) AssetDatabase.CreateFolder("Assets/Materials", "TemporalBinding");
        string path = $"{CartellaMateriali}/{nome}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", colore);
        mat.SetFloat("_Smoothness", lucidita);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
