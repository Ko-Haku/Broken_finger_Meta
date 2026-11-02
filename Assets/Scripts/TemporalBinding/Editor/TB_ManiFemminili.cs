using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Avatar con le mani femminili nella scena aperta (pensato per TB_NuoviStrumenti).
// Per ogni avatar maschile (AVATAR_Mano, AVATAR_Tool) crea un clone AVATAR_ManoF / AVATAR_ToolF con
// tutta la configurazione (VRIK, retargeting, torsione, pinzetta...) e ci mette le mesh di
// Avatar_ManoF.fbx / Avatar_ToolF.fbx (Blender/costruisci_avatar_tb.py con TB_MANI=F):
//  - ogni SkinnedMeshRenderer con lo stesso nome prende mesh, materiali e ossa dal FBX femminile
//    (la manica è allargata e il braccio dell'avatar tagliato sulla misura del braccio femminile);
//  - le ossa da avambraccio in giù tornano alla posa di riposo del FBX femminile (le articolazioni
//    delle dita stanno dove sono nella mano femminile, altrimenti la pelle si deformerebbe).
// I cloni vengono aggiunti alle liste "avatar" di TB_SwapEffettore (T li scorre), TB_PrimaPersona,
// TB_CalibrazioneCorpo e TB_Diagnostica. Rieseguibile: i cloni vecchi vengono ricostruiti.
public static class TB_ManiFemminili
{
    const string Cartella = "Assets/MODELS/TemporalBinding/Avatar_TB/";
    static readonly (string sorgente, string fbx, string clone)[] Coppie =
    {
        ("AVATAR_Mano", "Avatar_ManoF.fbx", "AVATAR_ManoF"),
        ("AVATAR_Tool", "Avatar_ToolF.fbx", "AVATAR_ToolF"),
    };

    [MenuItem("Tools/Temporal Binding/Avatar con mani femminili (scena aperta)")]
    public static void Esegui()
    {
        var log = new StringBuilder("\n===== TB_ManiFemminili =====\n");
        var scena = EditorSceneManager.GetActiveScene();
        var nuovi = new List<GameObject>();

        foreach (var (nomeSrc, fbxNome, nomeClone) in Coppie)
        {
            var src = Trova(nomeSrc);
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Cartella + fbxNome);
            if (!src || !fbx) { log.AppendLine($"!! manca {nomeSrc} in scena o {fbxNome}"); continue; }

            var vecchio = Trova(nomeClone);
            if (vecchio) { TogliDalleListe(vecchio); Object.DestroyImmediate(vecchio); }

            bool eraAttivo = src.activeSelf;
            var clone = Object.Instantiate(src, src.transform.parent);
            clone.name = nomeClone;
            clone.transform.SetSiblingIndex(src.transform.GetSiblingIndex() + 1);
            clone.SetActive(false);
            Undo.RegisterCreatedObjectUndo(clone, "Avatar mani femminili");

            var ossaClone = Mappa(clone.transform);
            var ossaFbx = Mappa(fbx.transform);

            // 1. posa di riposo femminile per avambracci, polsi e dita
            int ossa = 0;
            foreach (var lato in new[] { "Left", "Right" })
            {
                if (!ossaClone.TryGetValue($"mixamorig_{lato}ForeArm", out var fa)) continue;
                foreach (var t in fa.GetComponentsInChildren<Transform>(true))
                {
                    if (t == fa || !ossaFbx.TryGetValue(t.name, out var tf)) continue;   // pinzetta, anello, punte: restano
                    t.localPosition = tf.localPosition; t.localRotation = tf.localRotation;
                    if (!t.name.EndsWith("Hand")) t.localScale = tf.localScale;          // la scala della mano la gestisce TB_ManoRealistica
                    ossa++;
                }
            }

            // 2. mesh femminili sugli SkinnedMeshRenderer con lo stesso nome
            var smrFbx = fbx.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(r => r.name);
            int mesh = 0;
            foreach (var r in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smrFbx.TryGetValue(r.name, out var rf)) continue;
                r.sharedMesh = rf.sharedMesh;
                r.bones = rf.bones.Select(b => b && ossaClone.TryGetValue(b.name, out var c) ? c : null).ToArray();
                if (rf.rootBone && ossaClone.TryGetValue(rf.rootBone.name, out var rb)) r.rootBone = rb;
                r.sharedMaterials = rf.sharedMaterials;
                r.localBounds = rf.localBounds;
                mesh++;
            }
            clone.SetActive(false);
            nuovi.Add(clone);
            log.AppendLine($"  + {nomeClone} da {nomeSrc}: {mesh} mesh femminili, {ossa} ossa in posa femminile (sorgente attiva: {eraAttivo})");
        }

        // 3. liste degli avatar
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!mb || !(mb is TB_SwapEffettore || mb is TB_PrimaPersona || mb is TB_CalibrazioneCorpo || mb is TB_Diagnostica)) continue;
            var so = new SerializedObject(mb);
            var lista = so.FindProperty("avatar");
            if (lista == null || !lista.isArray) continue;
            foreach (var g in nuovi)
            {
                lista.InsertArrayElementAtIndex(lista.arraySize);
                lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = g;
            }
            so.ApplyModifiedProperties();
            log.AppendLine($"  {mb.GetType().Name}: avatar ora {lista.arraySize}");
        }

        EditorSceneManager.MarkSceneDirty(scena);
        EditorSceneManager.SaveScene(scena);
        Debug.Log(log.Append("===== fine =====").ToString());
    }

    static void TogliDalleListe(GameObject g)
    {
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!mb) continue;
            var so = new SerializedObject(mb);
            var lista = so.FindProperty("avatar");
            if (lista == null || !lista.isArray || lista.propertyType != SerializedPropertyType.Generic) continue;
            for (int i = lista.arraySize - 1; i >= 0; i--)
            {
                var e = lista.GetArrayElementAtIndex(i);
                if (e.propertyType == SerializedPropertyType.ObjectReference && e.objectReferenceValue == g)
                    lista.DeleteArrayElementAtIndex(i);
            }
            so.ApplyModifiedProperties();
        }
    }

    static GameObject Trova(string nome) =>
        Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
              .Where(t => t.name == nome && t.gameObject.scene.IsValid()).Select(t => t.gameObject).FirstOrDefault();

    static Dictionary<string, Transform> Mappa(Transform radice)
    {
        var d = new Dictionary<string, Transform>();
        foreach (var t in radice.GetComponentsInChildren<Transform>(true))
            if (!d.ContainsKey(t.name)) d[t.name] = t;
        return d;
    }
}
