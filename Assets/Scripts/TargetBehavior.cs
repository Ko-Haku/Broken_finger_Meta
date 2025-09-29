using UnityEngine;

public class TargetBehavior : MonoBehaviour
{
    public ExperimentRT experimentRT;    // nuovo manager

    private void Start()
    {
        if (experimentRT == null)
            experimentRT = GameObject.Find("EXPERIMENT MANAGER")?.GetComponent<ExperimentRT>();

        if (experimentRT == null)
            Debug.LogError("Nessun ExperimentRT trovato in scena!");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("effector"))
            return;

        if (experimentRT != null)
        {
           // experimentRT.RegisterTargetPopped();
            Debug.Log("Nuovo ExperimentRT → Target popped");
        }

        Destroy(gameObject); // rimuovi la pallina poppata
    }
}