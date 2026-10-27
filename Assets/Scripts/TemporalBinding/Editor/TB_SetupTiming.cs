using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Aggiunge TB_Timing alle scene TB e stampa un resoconto del costo di rendering.
// Batch: Unity.exe -batchmode -quit -projectPath ... -executeMethod TB_SetupTiming.Esegui
public static class TB_SetupTiming
{
    static readonly string[] Scene =
    {
        "Assets/Scenes/TemporalBinding_Pilota.unity",
        "Assets/Scenes/TB_NuoviStrumenti.unity",
    };

    [MenuItem("Tools/Temporal Binding/Setup timing e resoconto rendering")]
    public static void Esegui()
    {
        var sb = new StringBuilder("\n===== TB_SetupTiming =====\n");
        foreach (var path in Scene)
        {
            var scena = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            sb.AppendLine($"--- {path}");

            if (!Object.FindObjectsByType<TB_Timing>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any())
            {
                var go = new GameObject("TB_Timing");
                go.AddComponent<TB_Timing>();
                EditorSceneManager.MarkSceneDirty(scena);
                sb.AppendLine("  + TB_Timing aggiunto (80 Hz, CPU/GPU SustainedHigh)");
            }
            else sb.AppendLine("  TB_Timing già presente");

            foreach (var ovr in Object.FindObjectsByType<OVRManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ovr.enableDynamicResolution) { ovr.enableDynamicResolution = false; EditorUtility.SetDirty(ovr); }
                sb.AppendLine($"  OVRManager '{ovr.name}' attivo={ovr.isActiveAndEnabled} dynRes={ovr.enableDynamicResolution}");
            }

            // componenti del paradigma presenti
            string[] tipi = { "TB_ExperimentManager", "TB_PinchSensor", "TB_SliderRisposta", "TB_Logger", "TB_Cinematica", "TB_Lampadina" };
            foreach (var t in tipi)
            {
                var objs = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                 .Where(m => m && m.GetType().Name == t).ToList();
                sb.AppendLine($"  {t}: {(objs.Count == 0 ? "ASSENTE" : string.Join(", ", objs.Select(o => $"{o.name}{(o.isActiveAndEnabled ? "" : " (spento)")}")))}");
            }

            // costo: triangoli dei renderer attivi
            long tris = 0; int nRend = 0;
            var pesanti = new System.Collections.Generic.List<(string, long)>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                Mesh m = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (!m) continue;
                long n = 0;
                for (int i = 0; i < m.subMeshCount; i++) n += m.GetIndexCount(i) / 3;
                tris += n; nRend++;
                pesanti.Add((r.name + (r is SkinnedMeshRenderer ? " [skin]" : ""), n));
            }
            sb.AppendLine($"  Renderer attivi: {nRend}, triangoli: {tris:N0}");
            foreach (var (n, c) in pesanti.OrderByDescending(p => p.Item2).Take(8)) sb.AppendLine($"    {c,10:N0}  {n}");

            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"  Luce '{l.name}' {l.type} attiva={l.gameObject.activeInHierarchy} ombre={l.shadows} modo={l.lightmapBakeType}");
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"  Volume '{v.name}' attivo={v.isActiveAndEnabled} effetti={(v.sharedProfile ? string.Join("/", v.sharedProfile.components.Select(c => c.GetType().Name)) : "-")}");
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var d = c.GetUniversalAdditionalCameraData();
                sb.AppendLine($"  Camera '{c.name}' postFX={d.renderPostProcessing} AA={d.antialiasing} HDR={c.allowHDR}");
            }

            EditorSceneManager.SaveScene(scena);
        }

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp) sb.AppendLine($"URP '{urp.name}': renderScale={urp.renderScale} MSAA={urp.msaaSampleCount} HDR={urp.supportsHDR} " +
                               $"ombre={urp.supportsMainLightShadows}/{urp.supportsAdditionalLightShadows} distOmbre={urp.shadowDistance}");
        var urpAndroid = QualitySettings.GetRenderPipelineAssetAt(QualitySettings.GetQualityLevel());
        sb.AppendLine($"Quality level: {QualitySettings.names[QualitySettings.GetQualityLevel()]}, pipeline={(urpAndroid ? urpAndroid.name : "default")}");
        sb.AppendLine("===== fine =====");
        Debug.Log(sb.ToString());
    }
}
