using UnityEngine;
using System.Collections;

public class StickyEnableAfterDelay : MonoBehaviour
{
    [Header("Chi riaccendere")]
    public GameObject target;          // assegna qui ManoDX (NON essere figlio di target)

    [Header("Timing")]
    public float delaySeconds = 3f;    // aspetta 3s prima di riaccendere
    public float guardSeconds = 5f;    // per 5s garantisce che resti acceso

    void OnEnable()
    {
        StartCoroutine(GuardRoutine());
    }

    IEnumerator GuardRoutine()
    {
        if (target == null) yield break;

        // aspetta il delay iniziale
        yield return new WaitForSeconds(delaySeconds);

        // prima accensione
        if (!target.activeSelf) target.SetActive(true);

        // per un po' di tempo, se qualcuno lo spegne, lo riaccendiamo
        float t = 0f;
        while (t < guardSeconds)
        {
            if (!target.activeSelf) target.SetActive(true);
            t += Time.unscaledDeltaTime;   // non dipende dal timeScale
            yield return null;
        }
    }
}