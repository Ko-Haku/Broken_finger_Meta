using UnityEngine;

// Prima persona: nasconde la testa dell'avatar (scala quasi nulla all'osso della testa, trucco
// standard: le mesh del viso collassano, VRIK continua a usare la posizione dell'osso)
// e tiene spente le mani visive Meta, che il loro HandVisual riaccende da solo.
public class TB_PrimaPersona : MonoBehaviour
{
    public Animator[] avatar;
    public Oculus.Interaction.HandVisual[] visualMeta;

    void Start()
    {
        foreach (var a in avatar)
        {
            var testa = a ? a.GetBoneTransform(HumanBodyBones.Head) : null;
            if (testa) testa.localScale = Vector3.one * 1e-4f;
        }
    }

    void LateUpdate()
    {
        foreach (var v in visualMeta)
        {
            if (!v) continue;
            v.ForceOffVisibility = true;
            foreach (var r in v.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }
    }
}
