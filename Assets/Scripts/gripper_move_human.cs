using UnityEngine;

public class gripper_move_human : MonoBehaviour
{
    public Transform puntoA;
    public Transform puntoC;
    public Transform dito;
    public Transform pinz2;

    public float x;
    public float y;
    public float z;
    public float w;

    public float x2 = 0.6f;
    public float x3;
    public float distanza;
    public float distanza2;
    public float dist;
    public float incremento;
    public float incremento2;
    public float velocità;
    public float angle;

    private bool isColliding = false;

    void Start()
    {
        y = 0;
        velocità = 0.5f;
    }

    void Update()
    {
        Vector3 vectorAB = transform.position - puntoA.position;
        Vector3 vectorBC = puntoC.position - transform.position;

        
        angle = Vector3.Angle(vectorAB, vectorBC);
        dist = Vector3.Distance(puntoA.position, puntoC.position);
        distanza = dist * 100;
        distanza2 = dist * 100;
        distanza -= 1.6f;
        distanza2 -= x3;
        incremento = distanza / velocità;

        //if (!isColliding) // Only rotate if not colliding
        //{
            dito.localRotation = new Quaternion(x, y, z, w) * Quaternion.Euler(x2 * x * distanza2 * incremento, y, z) * new Quaternion(x, y, z, w);
            pinz2.localRotation = Quaternion.Euler(x2 * distanza * incremento, y, z);
            //pinz2.localRotation = Quaternion.Euler(x2 * angle *incremento, y, z);
        //}
    }

    // Trigger collision detection
    //private void OnTriggerEnter(Collider other)
    //{
    //    if (other.CompareTag("pinzettasu"))
    //    {
    //        isColliding = true;
    //    }
    //}

    //private void OnTriggerExit(Collider other)
    //{
    //    if (other.CompareTag("pinzettasu"))
    //    {
    //        isColliding = false;
    //    }
    //}
}
