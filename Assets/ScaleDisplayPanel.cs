using UnityEngine;
using TMPro;

public class ScaleDisplayPanel : MonoBehaviour
{
    [Header("Target Object")]
    public Transform targetObject;
    
    [Header("UI Elements")]
    public TextMeshProUGUI scaleXText;
    
    public void UpdateScaleDisplay()
    {
        if (targetObject != null)
        {
            Vector3 scale = targetObject.localScale;
            scaleXText.text = $"Scale: {scale.x:F2}";
        }
    }
}