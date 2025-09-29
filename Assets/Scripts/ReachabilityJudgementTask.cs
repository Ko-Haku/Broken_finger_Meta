
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;

public class ReachabilityJudgementTask : MonoBehaviour
{
    public GameObject reachTargetLeft_VeryNear;
    public GameObject reachTargetRight_VeryNear;
    public GameObject reachTargetLeft_Near;
    public GameObject reachTargetRight_Near;
    public GameObject reachTargetLeft_Far;
    public GameObject reachTargetRight_Far;
    public GameObject reachTargetLeft_VeryFar;
    public GameObject reachTargetRight_VeryFar;

    private Stopwatch timerRJT = new Stopwatch();

    private bool isOnreachTargetLeft_VeryNear;
    private bool isOnreachTargetRight_VeryNear;
    private bool isOnreachTargetLeft_Near;
    private bool isOnreachTargetRight_Near;
    private bool isOnreachTargetLeft_Far;
    private bool isOnreachTargetRight_Far;
    private bool isOnreachTargetLeft_VeryFar;
    private bool isOnreachTargetRight_VeryFar;

    private bool newTrial;

    public string subject_ID;
    public int subject_Code;
    public int trial = 0;
    private int randomCondition;
    private List<int> conditionSequence = new List<int>();
    private int numberTrials = 11;
    private int numberCondition = 8;

    public List<string> participantID = new List<string>();
    public List<int> participantCode = new List<int>();
    public List<int> nTrial = new List<int>();
    public List<string> conditionTrial = new List<string>();
    public List<int> keyPressed = new List<int>();
    public List<int> correct = new List<int>();
    public List<float> reactionTime = new List<float>();
    public List<int> day = new List<int>();
    public List<string> session = new List<string>();

    private string fileName = "";
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
   
    
    private InputDevice rightController;

  
  
    void Start()
    { InitializeRightController();
    
        reachTargetLeft_VeryNear.SetActive(false);
        reachTargetRight_VeryNear.SetActive(false);
        reachTargetLeft_Near.SetActive(false);
        reachTargetRight_Near.SetActive(false);
        reachTargetLeft_Far.SetActive(false);
        reachTargetRight_Far.SetActive(false);
        reachTargetLeft_VeryFar.SetActive(false);
        reachTargetRight_VeryFar.SetActive(false);

        conditionSequence = Enumerable.Range(1, numberCondition).SelectMany(condition => Enumerable.Repeat(condition, numberTrials)).ToList();
        ShuffleList(conditionSequence);

        fileName = Application.dataPath + "/RJT" + subject_ID + ".csv";

        using (TextWriter writer = new StreamWriter(fileName, false))
        {
            writer.WriteLine("Participant_ID, Participant_Code, n_Trial, Condition, Input, Correct, RTs");
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            UnityEngine.Debug.Log("Reachability Judgement Task started");
            StartCoroutine(SpawnReachTarget());
        }
    }

    // Update is called once per frame
    void Update()
    { 
        
        if (!rightController.isValid)
        {
            InitializeRightController();
            return;
        }
        if (OVRInput.GetDown(OVRInput.Button.Start))
        {
            
             UnityEngine.Debug.Log("pulsante 4 premuto");
        }
        if (rightController.TryGetFeatureValue(CommonUsages.primaryButton, out bool isA) && isA)
        {
            UnityEngine.Debug.Log("Pulsante A premuto (primaryButton)");
        }
      
            
            
            
            
        if (isOnreachTargetLeft_VeryNear)
        {
            reachTargetLeft_VeryNear.SetActive(true);
        }
        else if (isOnreachTargetLeft_VeryNear)
        {
            reachTargetLeft_VeryNear.SetActive(false);
        }

        if (isOnreachTargetRight_VeryNear)
        {
            reachTargetRight_VeryNear.SetActive(true);
        }
        else if (isOnreachTargetRight_VeryNear == false)
        {
            reachTargetRight_VeryNear.SetActive(false);
        }

        if (isOnreachTargetLeft_Near)
        {
            reachTargetLeft_Near.SetActive(true);
        }
        else if (isOnreachTargetLeft_Near == false)
        {
            reachTargetLeft_Near.SetActive(false);
        }

        if (isOnreachTargetRight_Near)
        {
            reachTargetRight_Near.SetActive(true);
        }
        else if (isOnreachTargetRight_Near == false)
        {
            reachTargetRight_Near.SetActive(false);
        }
        
        if (isOnreachTargetLeft_Far)
        {
            reachTargetLeft_Far.SetActive(true);
        }
        else if (isOnreachTargetLeft_Far)
        {
            reachTargetLeft_Far.SetActive(false);
        }

        if (isOnreachTargetRight_Far)
        {
            reachTargetRight_Far.SetActive(true);
        }
        else if (isOnreachTargetRight_Far)
        {
            reachTargetRight_Far.SetActive(false);
        }
        
        if (isOnreachTargetLeft_VeryFar)
        {
            reachTargetLeft_VeryFar.SetActive(true);
        }
        else if (isOnreachTargetLeft_VeryFar == false)
        {
            reachTargetLeft_VeryFar.SetActive(false);
        }

        if (isOnreachTargetRight_VeryFar)
        {
            reachTargetRight_VeryFar.SetActive(true);
        }
        else if (isOnreachTargetRight_VeryFar == false)
        {
            reachTargetRight_VeryFar.SetActive(false);
        }

        if (newTrial && (OVRInput.GetDown(OVRInput.Button.Three) || OVRInput.GetDown(OVRInput.Button.Four))) // 0 is left button = YES
        {
            newTrial = false;
            isOnreachTargetLeft_VeryNear = false;
            isOnreachTargetRight_VeryNear = false;
            isOnreachTargetLeft_Near = false;
            isOnreachTargetRight_Near = false;
            isOnreachTargetLeft_Far = false;
            isOnreachTargetRight_Far = false;
            isOnreachTargetLeft_VeryFar = false;
            isOnreachTargetRight_VeryFar = false;
            StopAllCoroutines();
            timerRJT.Stop();
            reactionTime.Add(timerRJT.ElapsedMilliseconds);
            if (Input.GetMouseButtonDown(0) && (randomCondition == 1 || randomCondition == 2 || randomCondition == 3 || randomCondition == 4))
            {
                keyPressed.Add(1);
                correct.Add(1);
            }
            else if (Input.GetMouseButtonDown(1) && (randomCondition == 1 || randomCondition == 2 || randomCondition == 3 || randomCondition == 4))
            {
                keyPressed.Add(2);
                correct.Add(0);
            }
            else if (Input.GetMouseButtonDown(0) && (randomCondition == 5 || randomCondition == 6 || randomCondition == 7 || randomCondition == 8))
            {
                keyPressed.Add(1);
                correct.Add(0);
            }
            else if (Input.GetMouseButtonDown(1) && (randomCondition == 5 || randomCondition == 6 || randomCondition == 7 || randomCondition == 8))
            {
                keyPressed.Add(2);
                correct.Add(1);
            }
            WriteTrialData();
            StartCoroutine(SpawnReachTarget());
        }
    }
        
    
    void InitializeRightController()
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, devices);

        if (devices.Count > 0)
        {
            rightController = devices[0];
            UnityEngine.Debug.Log("Controller destro trovato: " + rightController.name);
        }
        else
        {
            UnityEngine.Debug.LogWarning("Controller destro NON trovato.");
        }
    }
    private IEnumerator SpawnReachTarget()
    {
        if (trial < 88)
        {
            yield return new WaitForSeconds(2f);
            UnityEngine.Debug.Log("Trial:" + trial);
            participantID.Add(subject_ID);
            participantCode.Add(subject_Code);
            trial++;
            nTrial.Add(trial);

            randomCondition = conditionSequence[trial - 1];

            if (randomCondition == 1)
            {
                StartCoroutine(TimerLeftVeryNear());
                conditionTrial.Add("LVN");
            }
            else if (randomCondition == 2)
            {
                StartCoroutine(TimerRightVeryNear());
                conditionTrial.Add("RVN");
            }
            else if (randomCondition == 3)
            {
                StartCoroutine(TimerLeftNear());
                conditionTrial.Add("LN");
            }
            else if (randomCondition == 4)
            {
                StartCoroutine(TimerRightNear());
                conditionTrial.Add("RN");
            }
            else if (randomCondition == 5)
            {
                StartCoroutine(TimerLeftFar());
                conditionTrial.Add("LF");
            }
            else if (randomCondition == 6)
            {
                StartCoroutine(TimerRightFar());
                conditionTrial.Add("RF");
            }
            else if (randomCondition == 7)
            {
                StartCoroutine(TimerLeftVeryFar());
                conditionTrial.Add("LVF");
            }
            else if (randomCondition == 8)
            {
                StartCoroutine(TimerRightVeryFar());
                conditionTrial.Add("RVF");
            }
            newTrial = true;
            timerRJT.Restart();
        }
        else if (trial == 88)
        {
            UnityEngine.Debug.Log("Saved");
        }
    }
        
    private IEnumerator TimerLeftVeryNear()
    {
        isOnreachTargetLeft_VeryNear = true;
        yield return new WaitForSeconds(2);
        isOnreachTargetLeft_VeryNear = false;

        UnityEngine.Debug.Log("Too slow");
        newTrial = false;
        isOnreachTargetLeft_VeryNear = false;
        StopAllCoroutines();
        reactionTime.Add(2000);
        keyPressed.Add(0);
        correct.Add(0);
        WriteTrialData();
        StartCoroutine(SpawnReachTarget());
    }
        
    private IEnumerator TimerRightVeryNear()
    {
        isOnreachTargetRight_VeryNear = true;
        yield return new WaitForSeconds(2);
        isOnreachTargetRight_VeryNear = false;

        newTrial = false;
        isOnreachTargetRight_VeryNear = false;
        StopAllCoroutines();
        reactionTime.Add(2000);
        keyPressed.Add(0);
        correct.Add(0);
        WriteTrialData();
        StartCoroutine(SpawnReachTarget());
    }
    
    private IEnumerator TimerLeftNear()
    {
        isOnreachTargetLeft_Near = true;
        yield return new WaitForSeconds(2);
        isOnreachTargetLeft_Near = false;

        newTrial = false;
        isOnreachTargetLeft_Near = false;
        StopAllCoroutines();
        reactionTime.Add(2000);
        keyPressed.Add(0);
        correct.Add(0);
        WriteTrialData();
        StartCoroutine(SpawnReachTarget());
    }
    
    private IEnumerator TimerRightNear()
    {
        isOnreachTargetRight_Near = true;
        yield return new WaitForSeconds(2);
        isOnreachTargetRight_Near = false;

        newTrial = false;
        isOnreachTargetRight_Near = false;
        StopAllCoroutines();
        reactionTime.Add(2000);
        keyPressed.Add(0);
        correct.Add(0);
        WriteTrialData();
        StartCoroutine(SpawnReachTarget());
    }
    
    private IEnumerator TimerLeftFar()
    {
        isOnreachTargetLeft_Far = true;
        yield return new WaitForSeconds(2);
        isOnreachTargetLeft_Far = false;

        newTrial = false;
        isOnreachTargetLeft_Far = false;
        StopAllCoroutines();
        reactionTime.Add(2000);
        keyPressed.Add(0);
        correct.Add(0);
        WriteTrialData();
        StartCoroutine(SpawnReachTarget());
    }
    
    private IEnumerator TimerRightFar()
    {
        isOnreachTargetRight_Far = true;
        yield return new WaitForSeconds(2);
        isOnreachTargetRight_Far = false;

        newTrial = false;
        isOnreachTargetRight_Far = false;
        StopAllCoroutines();
        reactionTime.Add(2000);
        keyPressed.Add(0);
        correct.Add(0);
        WriteTrialData();
        StartCoroutine(SpawnReachTarget());
    }
    
    private IEnumerator TimerLeftVeryFar()
    {
        isOnreachTargetLeft_VeryFar = true;
        yield return new WaitForSeconds(2);
        isOnreachTargetLeft_VeryFar = false;

        newTrial = false;
        isOnreachTargetLeft_VeryFar = false;
        StopAllCoroutines();
        reactionTime.Add(2000);
        keyPressed.Add(0);
        correct.Add(0);
        WriteTrialData();
        StartCoroutine(SpawnReachTarget());
    }
    
    private IEnumerator TimerRightVeryFar()
    {
        isOnreachTargetRight_VeryFar = true;
        yield return new WaitForSeconds(2);
        isOnreachTargetRight_VeryFar = false;

        newTrial = false;
        isOnreachTargetRight_VeryFar = false;
        StopAllCoroutines();
        reactionTime.Add(2000);
        keyPressed.Add(0);
        correct.Add(0);
        WriteTrialData();
        StartCoroutine(SpawnReachTarget());
    }
    
    private void WriteTrialData()
    {
        using (TextWriter writer = new StreamWriter(fileName, true))
        {
            int i = trial - 1;
            writer.WriteLine(
                $"{participantID[i]}, {participantCode[i]}, {nTrial[i]}, {conditionTrial[i]}, {keyPressed[i]}, {correct[i]}, {reactionTime[i]}");
        }
    }

    private void ShuffleList<T>(List<T> list)
    {
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}
