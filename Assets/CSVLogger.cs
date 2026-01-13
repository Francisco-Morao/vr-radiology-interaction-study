using System.IO;
using UnityEngine;

public class CSVLogger : MonoBehaviour
{
    public static CSVLogger Instance;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void SaveTrialResult(
        int userID,
        string technique,
        int trialNumber,
        bool success,
        float timeToSuccess,
        float X_rotation_error,
        float Y_rotation_error,
        float Z_rotation_error,
        float scale_error,
        float total_error,
        int manipulationCount
        )
    {

        string fileName =
            $"{userID}_{technique}_Trial{trialNumber}.csv";

        string path =
            Path.Combine(Application.persistentDataPath, fileName);

        using StreamWriter sw = new StreamWriter(path, false);

        sw.WriteLine(
            "UserID,Technique,Trial,Success,TimeToSuccess," +
            "TranslationErrorX,TranslationErrorY,TranslationErrorZ,ScaleError,TotalError,ManipulationCount");
    
        sw.WriteLine(
            $"{userID},{technique},{trialNumber}," +
            $"{success},{timeToSuccess:F2}," +
            $"{X_rotation_error:F2},{Y_rotation_error:F2}," +
            $"{Z_rotation_error:F2},{scale_error:F2},{total_error:F2}," +
            $"{manipulationCount}");
    }
}
