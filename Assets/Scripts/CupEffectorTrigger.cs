using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Mettere sul bicchiere (cup). Rileva quando un collider con tag "effector"
/// (es. la sfera sul GripPoint) entra/esce dal collider del bicchiere e
/// notifica il GameManager per mandare il trigger BNC alla TriggerStation.
/// </summary>
public class CupEffectorTrigger : MonoBehaviour
{
    [Tooltip("Tag del collider dell'effettore (sfera sul GripPoint).")]
    public string effectorTag = "effector";

    [Tooltip("Riferimento al GameManager per mandare il trigger BNC.")]
    public ReachGraspGameManager gameManager;

    [Header("Generic events (per altri listener, es. CCT_ReachExperiment)")]
    public UnityEvent onEffectorEnter;
    public UnityEvent onEffectorExit;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(effectorTag)) return;

        Debug.Log($"[CupEffectorTrigger] Effector ENTER — {other.name}");

        if (gameManager != null)
            gameManager.OnEffectorTouchedCup();

        onEffectorEnter?.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(effectorTag)) return;

        Debug.Log($"[CupEffectorTrigger] Effector EXIT — {other.name}");

        if (gameManager != null)
            gameManager.OnEffectorReleasedCup();

        onEffectorExit?.Invoke();
    }
}

