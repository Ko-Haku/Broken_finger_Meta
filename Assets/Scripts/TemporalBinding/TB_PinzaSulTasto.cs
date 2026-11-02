using UnityEngine;

// Pinzetta del Tool, dopo l'Animator (che apre solo la ganascia superiore, Bone.002):
//  1. apertura speculare: l'angolo di apertura viene diviso a metà tra le due ganasce
//     (Bone.002 sale, Bone.001 scende), stessa distanza tra le punte, presa centrata;
//  2. collisione col pulsante: quando le punte sono sul tasto non si chiudono oltre lo spessore del
//     pulsante meno la corsa del tasto; l'angolo minimo si cerca sulla geometria vera delle ganasce.
// Ordine 32600: dopo VRIK/retargeting e dopo il replay delle clip passive (32500): legge l'angolo della
// ganascia superiore (Animator dal vivo o clip) e lo divide tra le due ganasce, con il blocco sul tasto.
[DefaultExecutionOrder(32600)]
public class TB_PinzaSulTasto : MonoBehaviour
{
    public TB_PinchSensor sensore;
    public Transform ganasciaSup;            // Bone.002 (animata)
    public Transform ganasciaInf;            // Bone.001 (ferma nell'Animator)
    public Transform puntaSup;               // Bone.002_end
    public Transform puntaInf;               // Bone.001_end
    [Tooltip("Angolo (gradi, X locale) della ganascia superiore a pinzetta chiusa")]
    public float angoloChiuso = 4f;
    public bool speculare = true;

    Quaternion riposoInf;
    bool pronto;

    void Awake()
    {
        if (!ganasciaSup || !ganasciaInf) { enabled = false; return; }
        riposoInf = ganasciaInf.localRotation;
    }

    void LateUpdate()
    {
        float a = ganasciaSup.localEulerAngles.x;
        if (a > 180f) a -= 360f;
        float apertura = Mathf.Max(0f, a - angoloChiuso);

        if (!pronto && sensore && puntaSup && puntaInf)
        {
            Posa(0f);
            sensore.gapChiusoTool = Gap();
            pronto = true;
            Debug.Log($"[TB] Pinzetta: punte a {sensore.gapChiusoTool * 1000:F1} mm da chiusa, bloccata sul tasto a {sensore.GapMinimoTool * 1000:F1} mm");
        }

        if (pronto && sensore.VicinoAlClicker && sensore.ConTool)
        {
            Posa(apertura);
            if (Gap() < sensore.GapMinimoTool)
            {
                // ricerca binaria dell'apertura minima che lascia le punte fuori dal pulsante
                float lo = apertura, hi = 150f;
                for (int i = 0; i < 14; i++)
                {
                    float m = (lo + hi) * 0.5f;
                    Posa(m);
                    if (Gap() < sensore.GapMinimoTool) lo = m; else hi = m;
                }
                apertura = hi;
            }
        }
        Posa(apertura);
    }

    void Posa(float apertura)
    {
        if (speculare)
        {
            ganasciaSup.localEulerAngles = new Vector3(angoloChiuso + apertura * 0.5f, 0f, 0f);
            ganasciaInf.localRotation = riposoInf * Quaternion.Euler(-apertura * 0.5f, 0f, 0f);
        }
        else
        {
            ganasciaSup.localEulerAngles = new Vector3(angoloChiuso + apertura, 0f, 0f);
            ganasciaInf.localRotation = riposoInf;
        }
    }

    float Gap() => Vector3.Distance(puntaSup.position, puntaInf.position);
}
