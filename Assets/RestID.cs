using UnityEngine;

public class RestID : MonoBehaviour
{
    [SerializeField] private bool resetIndex;

    private static string USER_ID_KEY = "RVUserID";

    void Start()
    {
        if (resetIndex)
        {
            if (PlayerPrefs.HasKey(USER_ID_KEY))
                PlayerPrefs.SetInt(USER_ID_KEY, 0);
        }
    }

}
