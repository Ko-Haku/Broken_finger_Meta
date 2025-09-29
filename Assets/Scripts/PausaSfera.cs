using UnityEngine;

public class PausaSfera : MonoBehaviour
{
    [Tooltip("Script del manager che implementa IExperimentManager")]
    public MonoBehaviour experimentManagerRaw;

    [Tooltip("Renderer della sfera (assegnalo manualmente)")]
    public Renderer rend;

    public Material materialeAttivo;
    public Material materialeDefault;

    private IExperimentManager experimentManager;
    private bool attiva = false;

    void Start()
    {
        if (rend == null)
        {
            Debug.LogError("❌ Renderer non assegnato nella PausaSfera!");
        }
        else
        {
            rend.material = materialeDefault;
        }

        if (experimentManagerRaw != null && experimentManagerRaw is IExperimentManager)
        {
            experimentManager = (IExperimentManager)experimentManagerRaw;
        }
        else
        {
            Debug.LogWarning("⚠️ experimentManagerRaw non assegnato o non implementa IExperimentManager.");
        }
    }

    public void AttivaSfera()
    {
        if (rend != null)
        {
            attiva = true;
            rend.material = materialeAttivo;
        }
    }

    public void DisattivaSfera()
    {
        if (rend != null)
        {
            attiva = false;
            rend.material = materialeDefault;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!attiva || experimentManager == null) return;
        if (!other.CompareTag("pinzetta")) return;

        DisattivaSfera();
        experimentManager.SegnalaPronto();
        Debug.Log("🟢 Sfera toccata: pronto segnalato.");
    }
}
