using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using System.IO;
using System.Text;

public class TrialManager : MonoBehaviour
{
    public TextMeshProUGUI timerText;

    [SerializeField] private GameObject myButton;
    [SerializeField] private Canvas canvasToDestroy;

    [Header("Enable On Timer Start")]
    [SerializeField] private GameObject[] objectsToEnable;

    [Header("Spawn On Timer Start")]
    [SerializeField] private GameObject[] prefabsToSpawn;
    [SerializeField] private Transform spawnParent;

    [Header("Random Image Selection")]
    [SerializeField] private GameObject image1;
    [SerializeField] private GameObject image2;
    [SerializeField] private TextMeshProUGUI targetScaleText;

    private float elapsedTime;
    private bool timerRunning = false;
    private const float MAX_TIME = 90f;
    private bool hasFinished = false;

    private static int lastImageUsed = -1; // -1 means no image used yet

    void Update()
    {
        if (!timerRunning || hasFinished) return;

        elapsedTime += Time.deltaTime;
        
        // UPDATE THE TIMER TEXT (COUNTING UP)
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(elapsedTime / 60f);
            int seconds = Mathf.FloorToInt(elapsedTime % 60f);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }

        if (elapsedTime >= MAX_TIME)
        {
            FinishScene();
        }
    }

    public void StartTimer()
    {
        elapsedTime = 0f;
        timerRunning = true;
        hasFinished = false;

        if (myButton != null)
            Destroy(myButton);

        if (canvasToDestroy != null)
            Destroy(canvasToDestroy.gameObject);

        // SET THE BINGO INDEX FIRST, BEFORE SPAWNING!
        SelectAndShowImage();

        foreach (GameObject obj in objectsToEnable)
        {
            if (obj != null)
                obj.SetActive(true);
        }

        // Spawn prefabs AFTER setting the bingo index
        foreach (GameObject prefab in prefabsToSpawn)
        {
            if (prefab != null)
            {
                GameObject spawned = Instantiate(prefab, Vector3.zero, Quaternion.identity, spawnParent);
                
                // Force the cube to update its bingo configuration IMMEDIATELY after spawn
                CubeYAxisRotation cube = spawned.GetComponent<CubeYAxisRotation>();
                if (cube != null)
                {
                    Debug.Log($"Forcing cube to use bingo index {TrialData.currentBingoIndex}");
                    cube.UpdateActiveBingo();
                }
            }
        }

        Debug.Log($"Timer started, objects loaded, currentBingoIndex = {TrialData.currentBingoIndex}");
    }

    void SelectAndShowImage()
    {
        // If this is the first attempt of a new trial set, randomly choose
        if (TrialData.trialCount == 0)
        {
            lastImageUsed = Random.Range(0, 2); // 0 or 1
            TrialData.currentBingoIndex = lastImageUsed;
            Debug.Log($"First trial: Selected bingo index {TrialData.currentBingoIndex}");
        }
        else if (TrialData.trialCount == 1)
        {
            // Second attempt: use the other image
            lastImageUsed = (lastImageUsed == 0) ? 1 : 0;
            TrialData.currentBingoIndex = lastImageUsed;
            Debug.Log($"Second trial: Switched to bingo index {TrialData.currentBingoIndex}");
        }

        // Show the selected image, hide the other
        if (lastImageUsed == 0)
        {
            if (image1 != null) image1.SetActive(true);
            if (image2 != null) image2.SetActive(false);
            if (targetScaleText != null) targetScaleText.text = "Target Scale: ±3.6";
            Debug.Log("Showing Image 1 (Bingo 0) - Scale 3.6");
        }
        else
        {
            if (image1 != null) image1.SetActive(false);
            if (image2 != null) image2.SetActive(true);
            if (targetScaleText != null) targetScaleText.text = "Target Scale: ±4.5";
            Debug.Log("Showing Image 2 (Bingo 1) - Scale 4.5");
        }
    }

    public void FinishScene()
    {
        Debug.Log("Finishing scene...");
        if (hasFinished) return;

        hasFinished = true;
        timerRunning = false;

        elapsedTime = Mathf.Min(elapsedTime, MAX_TIME);

        bool success = elapsedTime < MAX_TIME;
        SaveResult(success, elapsedTime);

        if (TrialData.mode != -1)
        {
            Scene currentScene = SceneManager.GetActiveScene();
            Debug.Log("Current scene name: " + currentScene.name);
            
            if (TrialData.totalCount == 4)
            {
                resetTrialData(-1, 0);
                TrialData.totalCount = 0;
                SceneManager.LoadScene("BasicScene");
            }

            if (TrialData.mode == 1 && currentScene.name == "TrialScene")
            {
                if (TrialData.trialCount == 1)
                {
                    resetTrialData(1, TrialData.trialCount);
                    SceneManager.LoadScene("TrialScene");
                    return;
                }
                resetTrialData(0, 0);
                SceneManager.LoadScene("TrialScene2");
            }
            else if (TrialData.mode == 0 && currentScene.name == "TrialScene2")
            {
                if (TrialData.trialCount == 1)
                {
                    resetTrialData(0, TrialData.trialCount);
                    SceneManager.LoadScene("TrialScene2");
                    return;
                }
                resetTrialData(1, 0);
                SceneManager.LoadScene("TrialScene");
            }
        }
        else
        {
            Debug.Log("TrialData mode not set! This should not happen.");
        }
    }

    void SaveResult(bool success, float timeToSuccess)
    {
        TrialData.totalCount++;
        
        int[] errors = TrialData.errors;
        if (!success)
        {
            for (int i = 0; i < errors.Length; i++)
            {
                errors[i] = 100;
            }
        }
        int manipulationCount = TrialData.manipulationCount;

        TrialData.trialCount++;
        int trialIndex = TrialData.trialCount;
        string technique = TrialData.mode == 1 ? "Hands" : "Controllers";
         
        CSVLogger.Instance.SaveTrialResult(
            PlayerPrefs.GetInt("RVUserID"),
            technique,
            trialIndex,
            success,
            timeToSuccess,
            errors[0],
            errors[1],
            errors[2],
            errors[3],
            errors[4],
            manipulationCount
        );
    }

    void resetTrialData(int mode, int trialCount)
    {
        for (int i = 0; i < TrialData.errors.Length; i++)
        {
            TrialData.errors[i] = 0;
        }
        TrialData.manipulationCount = 0;
        TrialData.trialCount = trialCount;
        TrialData.mode = mode;
        
        // Don't reset currentBingoIndex here - it will be set by SelectAndShowImage
        Debug.Log($"Reset trial data: mode={mode}, trialCount={trialCount}");
    }
}