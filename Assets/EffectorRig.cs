using UnityEngine;

public class EffectorRig : MonoBehaviour
{
    public enum RigType { Hand, Pinza }

    [Header("Tipo rig")]
    public RigType rigType = RigType.Hand;

    [Header("Core Refs")]
    public Transform wrist;
    public Transform fingerSpawnPoint;
    public Transform helpfulTargetRef;    // Empty dietro (ancora balls)
    public Transform unhelpfulTargetRef;  // Empty davanti/punto pinch (fallback)

    [Header("Control (comuni/opzionali)")]
    [Tooltip("Driver distanza pollice–indice -> openAmount (mano) o Animator('Open') (pinza)")]
    public PinchOpenDriver_Interaction pinchDriver;      // può stare anche su un child
    [Tooltip("Distorsione indice (solo mano masked)")]
    public TIThumbIndexModifier tiIndexModifier;         // opzionale (mano)
    [Tooltip("Controller meccanico pinza (se lo usi ancora)")]
    public GripperController gripperController;          // opzionale (pinza)
    [Tooltip("Nasconde/mostra la falange indice XR (scala 0/1)")]
    public HandIndexHider indexHider;                    // opzionale (mano/pinza)

    [Header("CCT per questo rig")]
    public CCT_Task cctTask;

    [Header("Visual root (SOLO mesh/collider – NON camera/tracking)")]
    [Tooltip("Se vuoto, usa questo GameObject. Meglio assegnare il child che contiene SOLO i visual.")]
    public GameObject visualRoot;

    [Header("MANO: varianti visual")]
    [Tooltip("Visual baseline con 4 dita via tracking (sempre visibile insieme alla masked, se vuoi)")]
    public GameObject handTrackedRoot;    // baseline
    [Tooltip("Visual con indice guidato da AvatarMask/animazione (dito distorto/animato)")]
    public GameObject handMaskedRoot;     // helpful/unhelpful o stretch

    [Header("Componenti extra da ON/OFF (opz)")]
    [Tooltip("Es. Animator aggiuntivi, script custom. NON mettere componenti di tracking/camera.")]
    public Behaviour[] extraBehaviours;

    // cache
    Renderer[] _renderers;
    Collider[] _colliders;

    void Awake()
    {
        if (visualRoot == null) visualRoot = this.gameObject; // fallback
        _renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        _colliders = visualRoot.GetComponentsInChildren<Collider>(true);

        // Se non assegnati in Inspector, prova a recuperarli dai figli
        if (pinchDriver == null)       pinchDriver       = GetComponentInChildren<PinchOpenDriver_Interaction>(true);
        if (tiIndexModifier == null)   tiIndexModifier   = GetComponentInChildren<TIThumbIndexModifier>(true);
        if (gripperController == null) gripperController = GetComponentInChildren<GripperController>(true);
        if (indexHider == null)        indexHider        = GetComponentInChildren<HandIndexHider>(true);
    }

    /// Mostra/nasconde SOLO visual/collider/script extra (non tocca camera/tracking)
    public void ShowRig(bool on)
    {
        if (_renderers == null || _colliders == null)
        {
            _renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            _colliders = visualRoot.GetComponentsInChildren<Collider>(true);
        }
        foreach (var r in _renderers) if (r != null) r.enabled = on;
        foreach (var c in _colliders) if (c != null) c.enabled = on;

        if (extraBehaviours != null)
            foreach (var b in extraBehaviours) if (b != null) b.enabled = on;
    }

    /// Attiva/disattiva entrambe le varianti mano (se esistono)
    public void ShowBothHandVariants(bool on)
    {
        if (handTrackedRoot != null) handTrackedRoot.SetActive(on);
        if (handMaskedRoot  != null) handMaskedRoot.SetActive(on);
    }

    /// Commuta la variante mano: false=baseline/tracked; true=masked/distorta
    public void SetHandVariantDistorted(bool distorted)
    {
        if (handTrackedRoot == null && handMaskedRoot == null) return;
        if (handTrackedRoot != null) handTrackedRoot.SetActive(true);  // le vuoi entrambe visibili
        if (handMaskedRoot  != null) handMaskedRoot.SetActive(distorted);
    }
}
