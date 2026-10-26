using System.Globalization;
using System.IO;
using UnityEngine;

// In Play, con la pinzetta nella posa originale (TB_SegueAncora), premi G:
//  1. porta la flangia a contatto con la punta del moncherino (traslazione lungo l'avambraccio
//     + centratura sull'asse; l'orientamento originale non cambia)
//  2. aggancia la pinzetta all'osso della mano dell'avatar (resta solidale al moncherino)
//  3. salva la posa locale in aggancio_pinza.json, da riportare in editor e in Blender
// I punti di base e moncherino sono precalcolati in editor (PrecalcolaPunti): a runtime
// le mesh non sono leggibili.
public class TB_AgganciaPinza : MonoBehaviour
{
    public KeyCode tasto = KeyCode.G;
    public TB_SegueAncora supporto;          // Pinzetta_Originale
    public Transform mano;                   // mixamorig_RightHand dell'avatar tool
    public Transform medio1;                 // asse avambraccio/mano
    public Transform baseTransform;          // Gripper (base della pinzetta)
    public float sovrapposizione = 0.002f;   // m di compenetrazione flangia-moncherino

    [Header("Precalcolati in editor")]
    public Vector3[] puntiBase;              // nello spazio di baseTransform
    public Vector3[] puntiPuntaMoncherino;   // nello spazio di 'mano' (vertici del tappo, peso 100% mano)

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    void Update()
    {
        if (Input.GetKeyDown(tasto)) Aggancia();
    }

    public void Aggancia()
    {
        if (puntiBase == null || puntiBase.Length == 0 || puntiPuntaMoncherino == null || puntiPuntaMoncherino.Length == 0)
        {
            Debug.LogWarning("[TB] Aggancio pinza: punti non precalcolati");
            return;
        }
        Vector3 asse = (medio1.position - mano.position).normalized;

        float punta = float.MinValue; Vector3 centroPunta = Vector3.zero;
        foreach (var l in puntiPuntaMoncherino)
        {
            Vector3 p = mano.TransformPoint(l);
            punta = Mathf.Max(punta, Vector3.Dot(p - mano.position, asse)); centroPunta += p;
        }
        centroPunta /= puntiPuntaMoncherino.Length;

        float base_ = float.MaxValue;
        foreach (var l in puntiBase) base_ = Mathf.Min(base_, Vector3.Dot(baseTransform.TransformPoint(l) - mano.position, asse));
        Vector3 centroFlangia = Vector3.zero; int n = 0;
        foreach (var l in puntiBase)
        {
            Vector3 p = baseTransform.TransformPoint(l);
            if (Vector3.Dot(p - mano.position, asse) < base_ + 0.004f) { centroFlangia += p; n++; }
        }
        centroFlangia /= Mathf.Max(1, n);

        Vector3 lungo = asse * (punta - sovrapposizione - base_);
        Vector3 laterale = Vector3.ProjectOnPlane(centroPunta - centroFlangia, asse);
        Debug.Log($"[TB] Aggancio pinza: distanza flangia-moncherino {(base_ - punta) * 1000f:F1} mm, scarto laterale {laterale.magnitude * 1000f:F1} mm");

        supporto.enabled = false;
        var t = supporto.transform;
        t.position += lungo + laterale;
        t.SetParent(mano, true);

        string json = "{\n" +
            $"  \"localPosition\": [{F(t.localPosition.x)}, {F(t.localPosition.y)}, {F(t.localPosition.z)}],\n" +
            $"  \"localRotation\": [{F(t.localRotation.x)}, {F(t.localRotation.y)}, {F(t.localRotation.z)}, {F(t.localRotation.w)}],\n" +
            $"  \"localScale\": [{F(t.localScale.x)}, {F(t.localScale.y)}, {F(t.localScale.z)}]\n}}\n";
        string path = Path.Combine(Application.dataPath, "Scripts", "TemporalBinding", "aggancio_pinza.json");
        File.WriteAllText(path, json);
        Debug.Log($"[TB] Pinza agganciata all'osso della mano. Posa salvata in {path}");
    }

    static string F(float v) => v.ToString("R", Inv);
}
