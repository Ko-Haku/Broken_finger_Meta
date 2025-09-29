using UnityEngine;

public class ForceEnable : MonoBehaviour
{
    [Header("Target da riaccendere in Play")]
    public GameObject target;

    void Start()
    {
        target = this.gameObject;
        if (target != null)
        {
            target.SetActive(true);
            Debug.Log($"ForceEnable: riattivato {target.name} in Start()");
        }
    }

    void OnEnable()
    {
        if (target != null)
            target.SetActive(true);
    }
}