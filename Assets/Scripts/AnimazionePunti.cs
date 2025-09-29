using UnityEngine;

public class AnimazionePunti : MonoBehaviour
{
    public Experiment esperimento;
    public Transform puntoA;
    public Transform puntoB;
   
    public Animator animatoreA;
    //public Animator animatoreB;
    public float distanza;
    public string parametroAnimazione = "Proporzionale";
    public float multiplier=1;
    //public float offset1 = 0.2f;
    public float offset2 = 0f;
    public float multi = 0f;
    // public float multi2 = 0f;
    public void Start()
    {
       
    }
    void Update()
    {
        multi = esperimento.fingerMultiplier;
        distanza = Vector3.Distance(puntoA.position, puntoB.position)*multiplier;
        float valoreAnimazione = distanza;

        animatoreA.SetFloat(parametroAnimazione, (valoreAnimazione-offset2)*multi);
       // animatoreB.SetFloat(parametroAnimazione, (valoreAnimazione - offset1)*multi2);
    }
}
