using UnityEngine;

public class TrialData : MonoBehaviour
{
    public static TrialData Instance;

    public static int totalCount = 0;
    public static int[] errors = new int[5] { 0, 0, 0, 0, 0 }; 
    // [0] = X rotation error (%)
    // [1] = Y rotation error (%)
    // [2] = Z rotation error (%)
    // [3] = Scale error (%)
    // [4] = Average error (arithmetic mean of all 4 errors)
    public static int mode = -1;
    
    // trial counts
    public static int trialCount = 0;

    // manipulation counts
    public static int manipulationCount = 0;

    public static int currentBingoIndex = -1;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // survives scene changes
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
