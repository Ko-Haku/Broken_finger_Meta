using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

public class ReachabilityJT : MonoBehaviour
{
    public TextMeshProUGUI fine;
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

    public int trial = 0;
    private int randomCondition;
    private List<int> conditionSequence = new List<int>();
    private int numberTrials = 11;
    private int numberCondition = 8;

    private List<int> nTrial = new List<int>();
    private List<string> conditionTrial = new List<string>();
    private List<int> keyPressed = new List<int>();
    private List<int> correct = new List<int>();
    private List<float> reactionTime = new List<float>();

    private string participantID;
    private string condizione;
    private string scenaProvenienza;
    private string fileName = "";

    private InputDevice rightController;

    void Start()
    {
        InitializeRightController();

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

        // ✅ Dati da CondizioneStato
        participantID = CondizioneStato.idSoggetto;
        condizione = CondizioneStato.condizioneAttuale;
        scenaProvenienza = CondizioneStato.scenaPrecedente;

        fileName = Application.dataPath + $"/RJT_{participantID}_{condizione}_{scenaProvenienza}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";

        using (TextWriter writer = new StreamWriter(fileName, false))
        {
            writer.WriteLine("Participant_ID,Condizione,ScenaProvenienza,n_Trial,Condition,Input,Correct,RTs");
        }
    }

    void Update()
    {
        if (!rightController.isValid)
        {
            InitializeRightController();
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            UnityEngine.Debug.Log("Reachability Judgement Task started");
            StartCoroutine(SpawnReachTarget());
        }

        UpdateTargetVisibility();

        if (newTrial && (IsButtonAPressed() || IsButtonBPressed()))
        {
            newTrial = false;
            timerRJT.Stop();

            ResetTargetFlags();
            StopAllCoroutines();
            reactionTime.Add(timerRJT.ElapsedMilliseconds);

            if (IsButtonAPressed() && (randomCondition >= 1 && randomCondition <= 4))
            {
                keyPressed.Add(1); correct.Add(1);
            }
            else if (IsButtonBPressed() && (randomCondition >= 1 && randomCondition <= 4))
            {
                keyPressed.Add(2); correct.Add(0);
            }
            else if (IsButtonAPressed() && (randomCondition >= 5 && randomCondition <= 8))
            {
                keyPressed.Add(1); correct.Add(0);
            }
            else if (IsButtonBPressed() && (randomCondition >= 5 && randomCondition <= 8))
            {
                keyPressed.Add(2); correct.Add(1);
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

    void UpdateTargetVisibility()
    {
        var targets = new (GameObject obj, bool state)[]
        {
            (reachTargetLeft_VeryNear, isOnreachTargetLeft_VeryNear),
            (reachTargetRight_VeryNear, isOnreachTargetRight_VeryNear),
            (reachTargetLeft_Near, isOnreachTargetLeft_Near),
            (reachTargetRight_Near, isOnreachTargetRight_Near),
            (reachTargetLeft_Far, isOnreachTargetLeft_Far),
            (reachTargetRight_Far, isOnreachTargetRight_Far),
            (reachTargetLeft_VeryFar, isOnreachTargetLeft_VeryFar),
            (reachTargetRight_VeryFar, isOnreachTargetRight_VeryFar),
        };

        foreach (var (obj, state) in targets)
        {
            obj.SetActive(state);
        }
    }

    void ResetTargetFlags()
    {
        isOnreachTargetLeft_VeryNear = false;
        isOnreachTargetRight_VeryNear = false;
        isOnreachTargetLeft_Near = false;
        isOnreachTargetRight_Near = false;
        isOnreachTargetLeft_Far = false;
        isOnreachTargetRight_Far = false;
        isOnreachTargetLeft_VeryFar = false;
        isOnreachTargetRight_VeryFar = false;
    }

    bool IsButtonAPressed()
    {
        return rightController.TryGetFeatureValue(CommonUsages.primaryButton, out bool pressed) && pressed;
    }

    bool IsButtonBPressed()
    {
        return rightController.TryGetFeatureValue(CommonUsages.secondaryButton, out bool pressed) && pressed;
    }

    private IEnumerator SpawnReachTarget()
    {
        
       
        if (trial < 88)
        {
            yield return new WaitForSeconds(2f);
            UnityEngine.Debug.Log("Trial: " + trial);

            trial++;
            nTrial.Add(trial);

            randomCondition = conditionSequence[trial - 1];

            switch (randomCondition)
            {
                case 1:
                    StartCoroutine(Timer(() => isOnreachTargetLeft_VeryNear = true));
                    conditionTrial.Add("LVN");
                    break;
                case 2:
                    StartCoroutine(Timer(() => isOnreachTargetRight_VeryNear = true));
                    conditionTrial.Add("RVN");
                    break;
                case 3:
                    StartCoroutine(Timer(() => isOnreachTargetLeft_Near = true));
                    conditionTrial.Add("LN");
                    break;
                case 4:
                    StartCoroutine(Timer(() => isOnreachTargetRight_Near = true));
                    conditionTrial.Add("RN");
                    break;
                case 5:
                    StartCoroutine(Timer(() => isOnreachTargetLeft_Far = true));
                    conditionTrial.Add("LF");
                    break;
                case 6:
                    StartCoroutine(Timer(() => isOnreachTargetRight_Far = true));
                    conditionTrial.Add("RF");
                    break;
                case 7:
                    StartCoroutine(Timer(() => isOnreachTargetLeft_VeryFar = true));
                    conditionTrial.Add("LVF");
                    break;
                case 8:
                    StartCoroutine(Timer(() => isOnreachTargetRight_VeryFar = true));
                    conditionTrial.Add("RVF");
                    break;
            }

            newTrial = true;
            timerRJT.Restart();
        }
        else if (trial == 88)
        {
            fine.text = "Task Completato ";
            UnityEngine.Debug.Log("Task completato e salvato.");
        }
    }

    private IEnumerator Timer(System.Action activateFlag)
    {
        activateFlag.Invoke();
        yield return new WaitForSeconds(2);
        ResetTargetFlags();

        newTrial = false;
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
            writer.WriteLine($"{participantID},{condizione},{scenaProvenienza},{nTrial[i]},{conditionTrial[i]},{keyPressed[i]},{correct[i]},{reactionTime[i]}");
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
