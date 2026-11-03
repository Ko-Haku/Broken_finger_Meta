using UnityEngine;

public class ruotacubo : MonoBehaviour
{ public float speed=1;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
       
        transform.Rotate(new Vector3(15, 30, 45) * Time.deltaTime*speed);
    }
}
