using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit;
using System;

public class SceneLoader : MonoBehaviour
{
    public string sceneName = "";

    private XRSimpleInteractable simpleInteractable;

    private static string USER_ID_KEY = "RVUserID";
    private int currentUserID;

    //mode 1 hands first mode 0 controllers first
    private int MODE = -1;

    void Awake()
    {
        simpleInteractable = GetComponent<XRSimpleInteractable>();
        
        if (simpleInteractable != null)
        {
            simpleInteractable.selectEntered.AddListener(OnSelected);
        }
        else
        {
            Debug.LogError("XRSimpleInteractable not found on " + gameObject.name);
        }

        currentUserID = !PlayerPrefs.HasKey(USER_ID_KEY) ? 0 : PlayerPrefs.GetInt(USER_ID_KEY);
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        Debug.Log("Button selected, loading scene: " + sceneName);
        LoadSceneByName(sceneName);
    }

    public void LoadSceneByName(string targetScene)
    {

        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogError("Target scene name is null or empty.");
            return;
        }

        if (targetScene == SceneManager.GetActiveScene().name)
        {
            Debug.Log("Already in the target scene: " + targetScene);
            return;
        }

        if (targetScene == "Trial")
        {
            if (TrialData.mode != -1)
            {
                MODE = TrialData.mode;
                Debug.Log("Loaded MODE from SessionData: " + MODE);
            }
            else
            {
                MODE = UnityEngine.Random.Range(0, 2); //0 or 1 
                Debug.Log("Assigned random MODE: " + MODE);
                TrialData.mode = MODE;
            }

            if (MODE == 1) //hands first
            {
                Debug.Log("Setting sceneName to TrialScene for hands first mode.");
                sceneName = "TrialScene";
            } else {
                Debug.Log("Setting sceneName to TrialScene2 for controllers first mode.");
                sceneName = "TrialScene2";   
            }
            
            SaveID();            
            SceneManager.LoadScene(sceneName);
            Debug.Log("Setting up parameters for TrialScene.");

            return;
        }
        else if (targetScene == "TutorialScene")
        {
            Debug.Log("Setting up parameters for TutorialScene.");
            // e.g., GameSettings.Instance.SetTutorialMode(true);
            SceneManager.LoadScene(targetScene);
            return;
        }
        SceneManager.LoadScene(targetScene);
    }

    void SaveID()
    {   
        // First time user - assign ID
        currentUserID++;
        
        PlayerPrefs.SetInt(USER_ID_KEY, currentUserID);
        PlayerPrefs.Save(); // Important: Save to disk
        
        Debug.Log($"New User! Assigned ID: {currentUserID}");
    }

    void OnDestroy()
    {
        if (simpleInteractable != null)
        {
            simpleInteractable.selectEntered.RemoveListener(OnSelected);
        }
    }


}

