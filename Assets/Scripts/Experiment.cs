using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Diagnostics;

public class Experiment : MonoBehaviour
{
    public GameObject sferabuia;
    public GameObject ballPrefab;
    public Transform fingerSpawnPoint;
    public Transform toolSpawnPoint;
    public Transform wrist;
    public Transform toolWrist;
    public GameObject finger;
   // public GameObject mani;
    public GameObject tool;
    public bool useToolCondition;
    public float fingerMultiplier;
    public int trialsPerBlock = 5;
    public int blocks = 3;
    public Stopwatch stopwatch = new Stopwatch();
    public int currentTrial;
    public int currentBlock;
    private List<string> logData;
    private bool experimentPhaseComplete;
    public int poppedBallsCount;
    public bool via = false;
    private CCT_Task cctTask;
    public string nome;
    public string cognome;
    public string numeroSoggetto;
    public List<GameObject> currentBalls = new List<GameObject>();
    public bool ballTouched;
    private int score;
    private long elmill;
    public KeyCode cctStartMeasurementKey = KeyCode.M;
    public float trialDuration = 3f; // Duration of each trial in seconds
    public int numberOfBalls = 10; // Number of balls to spawn in each trial
    public float ballSpacing = 0.001f; // 1 millimeter spacing between balls
    public float respawnDelay = 3f; // Delay for respawn in seconds
    private float multiplier;

    void Start()
    {
        logData = new List<string>();
        logData.Add("CodiceSoggetto,NumeroSoggetto,CondizioneSperimentale,Block,Trial,BallsPopped");
        UnityEngine.Debug.Log("Experiment started. Press 'S' to start.");

        cctTask = GetComponent<CCT_Task>();
        nome = ParticipantInfo.Instance.Codice_Soggetto;
        cognome = ParticipantInfo.Instance.Condizione;
        numeroSoggetto = ParticipantInfo.Instance.NumeroSoggetto;

        SetupExperimentalCondition();
        multiplier = fingerMultiplier;
    }

    void SetupExperimentalCondition()
    {
        if (useToolCondition)
        {
            finger.SetActive(false);
            tool.SetActive(true);
            UnityEngine.Debug.Log("Tool condition active. Tool enabled, Finger disabled.");
        }
        else
        {
            finger.SetActive(true);
            tool.SetActive(false);
            UnityEngine.Debug.Log("Finger condition active. Finger enabled, Tool disabled.");
        }
       // mani.SetActive(false);
    }

    void Update()
    {
       
        if (Input.GetKeyDown(KeyCode.S))
        {
            UnityEngine.Debug.Log("Key 'S' pressed. Starting experiment.");
            StartExperiment();
        }

        if (Input.GetKeyDown(cctStartMeasurementKey))
        {
            UnityEngine.Debug.Log("Starting CCT measurement.");
            StartCoroutine(RunCCTMeasurement());
        }
    }

    public void StartExperiment()
    {
        currentBlock = 0;
        StartCoroutine(ExperimentRoutine());
    }

    IEnumerator ExperimentRoutine()
    {
        UnityEngine.Debug.Log("Starting CCT measurement.");
        StartCoroutine(RunCCTMeasurement());
        yield return new WaitUntil(() => via);
        for (currentBlock = 0; currentBlock < blocks; currentBlock++)
        {
            UnityEngine.Debug.Log($"Starting Block {currentBlock + 1}");
            //fingerMultiplier = multiplier;
            yield return StartCoroutine(sferanera2());
            if (currentBlock > 0)
            {
                SetupExperimentalCondition();
            }

            yield return new WaitForSeconds(1);

            for (currentTrial = 0; currentTrial < trialsPerBlock; currentTrial++)
            {
                //UnityEngine.Debug.Log($"Starting Trial {currentTrial + 1} of Block {currentBlock + 1}");
                
                SpawnBalls(); // Show balls
               
                stopwatch.Start();

                float elapsedTime = 0f;
               
                while (stopwatch.ElapsedMilliseconds < trialDuration*1000 && !ballTouched)
                {
                   
                    yield return null; // Wait until the next frame
                }

                stopwatch.Stop();
            

                // Hide balls for 1 second
                CleanupRemainingBalls();
                UnityEngine.Debug.Log("Waiting for 1 second before respawning balls.");
                yield return new WaitForSeconds(1);

                var elmill = stopwatch.ElapsedMilliseconds;
                string ms = elmill.ToString();
                string condition = useToolCondition ? "Tool" : "Finger";
                string logEntry = $"{nome},{numeroSoggetto},{cognome},{currentBlock + 1},{currentTrial + 1},{poppedBallsCount}";
                logData.Add(logEntry);

                ballTouched = false;

                SaveLog();
                // Respawn balls for the next trial
                SpawnBalls();
                poppedBallsCount = 0; // Reset the popped balls count at the beginning of each trial
                stopwatch.Reset();
                yield return new WaitForSeconds(respawnDelay); // Wait for any additional delay
            }

            // Cleanup remaining balls at the end of the block
            UnityEngine.Debug.Log("End of block. Cleaning up remaining balls.");
            CleanupRemainingBalls();
            experimentPhaseComplete = true;
           
            // Wait for user to start the CCT task
            UnityEngine.Debug.Log($"Experiment phase complete. Press '{cctStartMeasurementKey}' to start CCT task.");
            via = false;
            yield return new WaitForSeconds(2);
            UnityEngine.Debug.Log("Starting CCT measurement.");
            StartCoroutine(RunCCTMeasurement());
            yield return new WaitUntil(() => via);

            experimentPhaseComplete = false;
            
        }

        SaveLog();
        UnityEngine.Debug.Log("Condition completed.");

        // If running in the editor, just log a message; otherwise quit the application.
#if UNITY_EDITOR
        UnityEngine.Debug.Log("Experiment finished in the editor. Simulation complete.");
        UnityEditor.EditorApplication.isPlaying = false; // Stops
#else
    Application.Quit();
#endif
    }

    public IEnumerator sferanera()
    {
        sferabuia.SetActive(true);
        fingerMultiplier = 0.8f;
        yield return new WaitForSeconds(2);
        sferabuia.SetActive(false);
        yield return new WaitForSeconds(1);

    } 
    public IEnumerator sferanera2()
    {
        sferabuia.SetActive(true);
        fingerMultiplier = multiplier;
        yield return new WaitForSeconds(2);
        sferabuia.SetActive(false);

        
    }
    void CleanupRemainingBalls()
    {
        UnityEngine.Debug.Log("Cleaning up remaining balls.");
        foreach (GameObject ball in currentBalls)
        {
            if (ball != null)
            {
                Destroy(ball);
            }
        }
        currentBalls.Clear();
    }

    IEnumerator RunCCTMeasurement()
    {
        yield return StartCoroutine(sferanera());
        //fingerMultiplier = 0.8f;
        yield return StartCoroutine(cctTask.StartMisura());
        SaveLog();
        if (currentBlock !=0)
        via = true;
    }

    private (Transform spawnPoint, Transform parentWrist) GetActiveSpawnPointAndWrist()
    {
        if (useToolCondition)
        {
            return (toolSpawnPoint, toolWrist);
        }
        else
        {
            return (fingerSpawnPoint, wrist);
        }
    }

    void SpawnBalls()
    {
        UnityEngine.Debug.Log("Spawning balls.");
        foreach (GameObject ball in currentBalls)
        {
            if (ball != null) Destroy(ball);
        }
        currentBalls.Clear();

        var (activeSpawnPoint, activeWrist) = GetActiveSpawnPointAndWrist();

        Vector3 spawnPosition = activeSpawnPoint.position;

        Vector3 rightDirection = activeWrist.right;  // "right" direction of the wrist
   
        Vector3 forwardDirection = activeWrist.forward;  // "forward" direction of the wrist

        for (int i = 0; i < numberOfBalls; i++)
        {
            GameObject ball = Instantiate(ballPrefab, spawnPosition, activeSpawnPoint.rotation, activeWrist);
            if (useToolCondition)
            {
                //ball.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                //spawnPosition += rightDirection * ballSpacing;
                //spawnPosition += forwardDirection * ballSpacing;
                spawnPosition -= forwardDirection * ballSpacing;
                spawnPosition -= activeWrist.up * ballSpacing;
            }
            else
            {
                spawnPosition += rightDirection * ballSpacing;
                
                spawnPosition -= activeWrist.up * ballSpacing;
                // settings per apertura laterale
                // spawnPosition -= forwardDirection * ballSpacing;
                // spawnPosition -= activeWrist.up * ballSpacing;
            }

            TargetBehavior ballScript = ball.GetComponent<TargetBehavior>();
            //ballScript.oldExperiment = this;

            currentBalls.Add(ball);
        }
    }

    
        public void HandleBallTouched(GameObject touchedBall)
        {
            if (currentBalls.Contains(touchedBall))
            {
                int ballIndex = currentBalls.IndexOf(touchedBall);
                Destroy(currentBalls[ballIndex]);
                currentBalls.RemoveAt(ballIndex);

                poppedBallsCount++; // Increment the count of popped balls
                ballTouched = true;

                UnityEngine.Debug.Log($"Ball touched. Balls popped: {poppedBallsCount}");
           
        }
        }
    

    void SaveLog()
    {
        string directoryPath = Path.Combine(Application.dataPath, "Reports");
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string filePath = Path.Combine(directoryPath, $"experiment_{nome}_{cognome}.csv");
        File.WriteAllLines(filePath, logData);
        UnityEngine.Debug.Log("Log saved to " + filePath);
    }
}
