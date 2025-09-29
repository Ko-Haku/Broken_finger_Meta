using UnityEngine;

public class EffectorRig : MonoBehaviour
{
    public enum RigType { Hand, Pinza }

    [Header("Tipo rig")]
    public RigType rigType = RigType.Hand;

    [Header("Core Refs")]
    public Transform wrist;
    public Transform fingerSpawnPoint;
    public Transform helpfulTargetRef;   // Empty dietro
    public Transform unhelpfulTargetRef; // Empty su punto pinch/pollice

    [Header("Control (mano, opzionali)")]
    public PinchOpenDriver_Interaction pinchDriver; // driver per OpenAmount (xr)
    public TIThumbIndexModifier tiIndexModifier;    // distorsione indice (solo HandMasked)

    [Header("CCT per questo rig")]
    public CCT_Task cctTask;

    [Header("Visual root (SOLO mesh/collider – NON camera/tracking)")]
    [Tooltip("Se vuoto usa questo GameObject. Meglio assegnare il child che contiene SOLO i visual.")]
    public GameObject visualRoot;

    [Header("MANO: varianti visual (opzionali)")]
    [Tooltip("Visual baseline con 4 dita via tracking")]
    public GameObject handTrackedRoot;     // baseline
    [Tooltip("Visual con indice guidato da AvatarMask/animazione")]
    public GameObject handMaskedRoot;      // helpful/unhelpful
    [Header("XR Index (per nasconderlo/mostrarlo)")]
    public HandIndexHider indexHider;  // <- aggiunto
    public GripperController gripperController;
    [Header("Componenti extra da ON/OFF (opz)")]
    [Tooltip("Es. Animator, GripperController. NON mettere componenti di tracking/camera.")]
    public Behaviour[] extraBehaviours;

    // cache
    Renderer[] _renderers;
    Collider[] _colliders;

    void Awake()
    {
        if (visualRoot == null) visualRoot = this.gameObject; // fallback
        _renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        _colliders = visualRoot.GetComponentsInChildren<Collider>(true);
    }

    /// Mostra/nasconde SOLO visual/collider/script extra (non tocca camera/tracking)
    public void ShowBothHandVariants(bool on)
    {
        if (handTrackedRoot != null) handTrackedRoot.SetActive(on);
        if (handMaskedRoot  != null) handMaskedRoot.SetActive(on);
    }
    public void ShowRig(bool on)
    {
        if (_renderers == null || _colliders == null)
        {
            _renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            _colliders = visualRoot.GetComponentsInChildren<Collider>(true);
        }
        Debug.Log($"[EffectorRig] {(on?"SHOW":"HIDE")} renderers on {name} (root={(visualRoot?visualRoot.name:"SELF")})");
        foreach (var r in _renderers) if (r != null) r.enabled = on;
        foreach (var c in _colliders) if (c != null) c.enabled = on;

        if (extraBehaviours != null)
            foreach (var b in extraBehaviours) if (b != null) b.enabled = on;
    }

    /// Commuta la variante mano: false=baseline/tracked; true=masked/distorta
    public void SetHandVariantDistorted(bool distorted)
    {
        if (handTrackedRoot == null && handMaskedRoot == null) return;
        if (handTrackedRoot != null) handTrackedRoot.SetActive(!distorted);
        if (handMaskedRoot  != null) handMaskedRoot.SetActive(distorted);
    }
    
}
