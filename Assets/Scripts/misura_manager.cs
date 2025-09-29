using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class misura_manager : MonoBehaviour
{
    public Material acceso;
    public Material spento;
    FMOD.Studio.EventInstance instance;
   // public grabbable statosfera;
    public bool stopped = false;
    [FMODUnity.EventRef]
    public string fmodEvent = null;
    public float parameterValue;
    private bool AllowFadeout = true;
    [SerializeField]
    private string[] collisionTag = null;
    [SerializeField]
    private float minCollisionVolume = 0.1f;
    [SerializeField]
    private float maxCollisionVelocity = 5f;
    [SerializeField]
    private bool useParameter;

    [SerializeField]
    private string parameterName;
    // Start is called before the first frame update
    void Start()
    {
        gameObject.GetComponent<Renderer>().material = spento;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            parameterValue = parameterValue - 0.1f;
            StartCoroutine(Soglia());
        
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            parameterValue = parameterValue + 0.1f;
            StartCoroutine(Soglia());
        
        }

    }

    public IEnumerator accendispegni_haptic()
    {
        via();
       // gameObject.GetComponent<Renderer>().material = acceso;
        yield return new WaitForSeconds(0.1f);
      //  gameObject.GetComponent<Renderer>().material = spento;
        stop();
        yield return new WaitForSeconds(0.1f);
      //  gameObject.GetComponent<Renderer>().material = acceso;
        via();
        yield return new WaitForSeconds(0.1f);
       // gameObject.GetComponent<Renderer>().material = spento;
        stop();
        yield return new WaitForSeconds(0.1f);
        via();
      //  gameObject.GetComponent<Renderer>().material = acceso;
        yield return new WaitForSeconds(0.1f);
       // gameObject.GetComponent<Renderer>().material = spento;
        stop();

    }
    public IEnumerator Soglia()
    {
       
        via();
       // gameObject.GetComponent<Renderer>().material = acceso;
        yield return new WaitForSeconds(0.1f);
       
        stop();

    }

    public IEnumerator accendispegni_luce()
    {
        //via();
        gameObject.GetComponent<Renderer>().material = acceso;
        yield return new WaitForSeconds(0.1f);
        gameObject.GetComponent<Renderer>().material = spento;
       // stop();
        yield return new WaitForSeconds(0.1f);
        gameObject.GetComponent<Renderer>().material = acceso;
       // via();
        yield return new WaitForSeconds(0.1f);
        gameObject.GetComponent<Renderer>().material = spento;
       // stop();
        yield return new WaitForSeconds(0.1f);
      //  via();
        gameObject.GetComponent<Renderer>().material = acceso;
        yield return new WaitForSeconds(0.1f);
        gameObject.GetComponent<Renderer>().material = spento;
       // stop();


    }

    public void via()
    {
        instance = FMODUnity.RuntimeManager.CreateInstance(fmodEvent);
        instance.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));

        if (useParameter)
        {
            instance.setParameterByName(parameterName, parameterValue);
        }

        instance.start();
        instance.release();
    }

    public void stop()
    {
        instance.stop(AllowFadeout ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);
        instance.release();
        instance.clearHandle();
    }
}
