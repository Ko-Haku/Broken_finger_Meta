using System.Collections;
using System.Linq;
using RootMotion.FinalIK;
using UnityEngine;

// Taglia dell'avatar adattata al partecipante: con il gomito reale appoggiato sul tavolo e la mano in posa,
// se il braccio reale è più lungo di quello dell'avatar il gomito virtuale resta sollevato (VRIK porta la
// mano dove è quella vera e il gomito si piazza dove può).
// G: per circa un secondo prova diverse scale uniformi dell'avatar e tiene quella che porta il gomito
// virtuale sul piano del tavolo. La scala è uniforme (niente stiramenti): braccio, busto, moncherino e
// pinzetta crescono insieme; la mano resta della grandezza reale (TB_ManoRealistica la tiene in unità mondo).
// Vale per tutti gli avatar (Mano, Tool). Da fare dopo il tavolo (V) e prima del pulsante (C), partecipante fermo.
public class TB_CalibraBraccio : MonoBehaviour
{
    public Transform pianoTavolo;
    [Tooltip("Altezza del centro del gomito sopra il piano quando l'avambraccio è appoggiato")]
    public float altezzaGomito = 0.035f;
    [Tooltip("Scala uniforme dell'avatar (radice): minimo e massimo provati")]
    public Vector2 intervallo = new Vector2(0.7f, 1.2f);
    public float passo = 0.01f;
    public KeyCode tasto = KeyCode.G;

    public float Scala { get; private set; } = -1f;
    bool inCorso;

    float TopTavolo => pianoTavolo ? pianoTavolo.GetComponentInChildren<Renderer>().bounds.max.y : transform.position.y;

    void Update()
    {
        if (Input.GetKeyDown(tasto) && !inCorso) StartCoroutine(Calibra());
    }

    IEnumerator Calibra()
    {
        var tutti = FindObjectsByType<VRIK>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var attivo = tutti.FirstOrDefault(v => v.isActiveAndEnabled && v.references.rightForearm);
        if (!attivo) { Debug.LogWarning("[TB] Calibrazione braccio: nessun VRIK attivo"); yield break; }
        inCorso = true;
        float bersaglio = TopTavolo + altezzaGomito;
        Transform gomito = attivo.references.rightForearm;
        Transform radice = attivo.transform;
        float prima = radice.localScale.x;

        float migliore = prima, errMin = float.MaxValue, yMigliore = 0f;
        for (float s = intervallo.x; s <= intervallo.y + 1e-4f; s += passo)
        {
            radice.localScale = Vector3.one * s;
            yield return null;                    // un frame per il solve con la nuova scala
            yield return new WaitForEndOfFrame(); // posa finale dopo VRIK e retargeting
            float err = Mathf.Abs(gomito.position.y - bersaglio);
            if (err < errMin) { errMin = err; migliore = s; yMigliore = gomito.position.y; }
        }

        Scala = migliore;
        foreach (var v in tutti)
        {
            v.transform.localScale = Vector3.one * migliore;
            v.solver.rightArm.armLengthMlp = 1f;
            v.solver.leftArm.armLengthMlp = 1f;
        }
        bool alLimite = migliore <= intervallo.x + 1e-3f || migliore >= intervallo.y - 1e-3f;
        Debug.Log($"[TB] Avatar scalato: {prima:F2} -> {migliore:F2}, gomito {(yMigliore - TopTavolo) * 100f:F1} cm sopra il tavolo (atteso {altezzaGomito * 100f:F1})" +
                  (alLimite ? " — al limite dell'intervallo: controlla la posa" : ". Ora calibra il pulsante (C)"));
        inCorso = false;
    }
}
