using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BurstBall : MonoBehaviour
{
    [HideInInspector] public ExperimentRT manager;
    [Tooltip("Lascia vuoto per accettare qualsiasi collider")]
    public string requiredTag = "effector";

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
        var rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
            return;

        if (manager != null) manager.OnBallPopped(gameObject);
        Destroy(gameObject);
    }
}