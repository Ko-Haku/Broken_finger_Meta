using System.Collections.Generic;
using UnityEngine;

public class CupGrabVolume : MonoBehaviour
{
    [SerializeField] private CupSwitchDriver2Bases cup;
    [SerializeField] private string handTag = "Hand";
    [SerializeField] private LayerMask handLayerMask; // opzionale

    [Header("Hand filter")]
    [Tooltip("Qualsiasi collider figlio di uno di questi Transform viene IGNORATO. " +
             "Tipicamente: assegna qui la root della mano sx per evitare che l'hand-tracking sinistro grabbi il cup.")]
    [SerializeField] private Transform[] ignoreRoots;

    [Header("Debug")]
    [Tooltip("Logga ogni Enter/Exit con il nome del collider e il path completo. Usalo per identificare cosa sta facendo registrare hand-in-volume in modo spurio.")]
    [SerializeField] private bool debugLogs = false;
    [Tooltip("Disegna il wireframe del trigger collider sempre (non solo quando selezionato). Verde=vuoto, giallo=hand dentro.")]
    [SerializeField] private bool drawGizmoAlways = true;

    private readonly HashSet<Collider> _handInside = new();

    /// <summary>Read-only view of the colliders currently inside the volume (for overlays/diagnostics).</summary>
    public IReadOnlyCollection<Collider> Inside => _handInside;

    private bool IsIgnored(Collider other)
    {
        if (ignoreRoots == null || ignoreRoots.Length == 0) return false;
        for (int i = 0; i < ignoreRoots.Length; i++)
        {
            if (ignoreRoots[i] == null) continue;
            if (other.transform.IsChildOf(ignoreRoots[i])) return true;
        }
        return false;
    }

    private bool IsHandCollider(Collider other)
    {
        if (IsIgnored(other)) return false;

        if (other.CompareTag(handTag)) return true;

        // layer-based (opzionale)
        if (((1 << other.gameObject.layer) & handLayerMask.value) != 0) return true;

        // fallback nomi XRHands
        if (other.name.StartsWith("XRHand_")) return true;

        // fallback nomi tweezers
        if (other.name.Contains("Tweezer") || other.name.Contains("Gripper") || other.name.Contains("Pinza")) return true;

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsHandCollider(other)) return;

        _handInside.Add(other);
        if (debugLogs) Debug.Log($"[CupGrabVolume] ENTER '{other.name}' (path: {GetPath(other.transform)}) → inside={_handInside.Count}");
        cup.SetHandInside(_handInside.Count > 0);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsHandCollider(other)) return;

        _handInside.Remove(other);
        if (debugLogs) Debug.Log($"[CupGrabVolume] EXIT  '{other.name}' → inside={_handInside.Count}");
        cup.SetHandInside(_handInside.Count > 0);
    }

    private static string GetPath(Transform t)
    {
        var sb = new System.Text.StringBuilder(t.name);
        while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
        return sb.ToString();
    }

    // Always-visible gizmo for the trigger collider on this GameObject.
    // Color: green when empty, yellow when something's inside.
    private void OnDrawGizmos()
    {
        if (!drawGizmoAlways) return;
        DrawColliderGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (drawGizmoAlways) return; // already drawn by OnDrawGizmos
        DrawColliderGizmo();
    }

    private void DrawColliderGizmo()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;

        Color c = (_handInside.Count > 0)
            ? new Color(1f, 0.85f, 0.1f, 0.9f)   // yellow: something inside
            : new Color(0.2f, 1f, 0.2f, 0.7f);   // green: idle
        Gizmos.color = c;

        var prevMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        switch (col)
        {
            case BoxCollider box:
                Gizmos.DrawWireCube(box.center, box.size);
                break;
            case SphereCollider sph:
                Gizmos.DrawWireSphere(sph.center, sph.radius);
                break;
            case CapsuleCollider cap:
                // Capsule: approximate with two spheres + connecting wireframe
                Vector3 axis = cap.direction == 0 ? Vector3.right : cap.direction == 1 ? Vector3.up : Vector3.forward;
                float half = Mathf.Max(0f, cap.height * 0.5f - cap.radius);
                Gizmos.DrawWireSphere(cap.center + axis * half, cap.radius);
                Gizmos.DrawWireSphere(cap.center - axis * half, cap.radius);
                break;
            default:
                // Fallback: draw bounds in WORLD space (not local), so reset matrix.
                Gizmos.matrix = prevMatrix;
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
                return;
        }

        Gizmos.matrix = prevMatrix;
    }
}