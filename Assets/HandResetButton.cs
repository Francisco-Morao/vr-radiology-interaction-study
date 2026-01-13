using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// A physical button that hands can poke/touch to reset the image
/// Attach this to a button GameObject (like a cube or sphere)
/// </summary>
public class HandResetButton : MonoBehaviour
{
    [Header("Target to Reset")]
    [Tooltip("The object with CubeYAxisRotation script")]
    [SerializeField] private GameObject targetObject;
    
    [Header("Button Settings")]
    [Tooltip("Color when not pressed")]
    [SerializeField] private Color normalColor = Color.red;
    
    [Tooltip("Color when pressed")]
    [SerializeField] private Color pressedColor = Color.green;
    
    [Tooltip("How long to hold before reset (seconds)")]
    [SerializeField] private float holdTime = 1.0f;
    
    [Header("Visual Feedback")]
    [SerializeField] private bool showDebug = true;
    
    private XRSimpleInteractable interactable;
    private Renderer buttonRenderer;
    private CubeYAxisRotation targetScript;
    private float pressStartTime = 0f;
    private bool isPressed = false;
    private Material buttonMaterial;

    void Start()
    {
        // Get or add components
        interactable = GetComponent<XRSimpleInteractable>();
        if (interactable == null)
        {
            interactable = gameObject.AddComponent<XRSimpleInteractable>();
        }
        
        // Setup renderer for visual feedback
        buttonRenderer = GetComponent<Renderer>();
        if (buttonRenderer != null)
        {
            buttonMaterial = buttonRenderer.material;
            buttonMaterial.color = normalColor;
        }
        
        // Add collider if needed
        if (GetComponent<Collider>() == null)
        {
            SphereCollider col = gameObject.AddComponent<SphereCollider>();
            col.radius = 0.15f;
        }
        
        // Get target script
        if (targetObject != null)
        {
            targetScript = targetObject.GetComponent<CubeYAxisRotation>();
        }
        
        // Subscribe to interaction events
        interactable.hoverEntered.AddListener(OnHoverEnter);
        interactable.hoverExited.AddListener(OnHoverExit);
        interactable.selectEntered.AddListener(OnPress);
        interactable.selectExited.AddListener(OnRelease);
        
        if (showDebug)
            Debug.Log($"Reset Button initialized | Target: {(targetObject != null ? targetObject.name : "NOT SET")}");
    }

    void Update()
    {
        if (isPressed)
        {
            float holdDuration = Time.time - pressStartTime;
            
            // Update color based on progress
            if (buttonRenderer != null)
            {
                float progress = Mathf.Clamp01(holdDuration / holdTime);
                buttonMaterial.color = Color.Lerp(pressedColor, Color.yellow, progress);
            }
            
            // Check if held long enough
            if (holdDuration >= holdTime)
            {
                TriggerReset();
                isPressed = false; // Prevent multiple resets
            }
            
            if (showDebug && Time.frameCount % 30 == 0)
            {
                float remaining = holdTime - holdDuration;
            }
        }
    }

    void OnHoverEnter(HoverEnterEventArgs args){}

    void OnHoverExit(HoverExitEventArgs args){}

    void OnPress(SelectEnterEventArgs args)
    {
        isPressed = true;
        pressStartTime = Time.time;
        
        if (buttonRenderer != null)
        {
            buttonMaterial.color = pressedColor;
        }
    }

    void OnRelease(SelectExitEventArgs args)
    {
        isPressed = false;
        
        if (buttonRenderer != null)
        {
            buttonMaterial.color = normalColor;
        }
    }

    void TriggerReset()
    {
        if (targetScript != null)
        {
            // Access the private ResetRotation method through reflection
            // Or we can make it public in CubeYAxisRotation
            var method = targetScript.GetType().GetMethod("ResetRotation", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                method.Invoke(targetScript, null);
                
                // Flash feedback
                if (buttonRenderer != null)
                {
                    StartCoroutine(FlashButton());
                }
            }
        }
    }

    System.Collections.IEnumerator FlashButton()
    {
        for (int i = 0; i < 3; i++)
        {
            buttonMaterial.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            buttonMaterial.color = normalColor;
            yield return new WaitForSeconds(0.1f);
        }
    }

    void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEnter);
            interactable.hoverExited.RemoveListener(OnHoverExit);
            interactable.selectEntered.RemoveListener(OnPress);
            interactable.selectExited.RemoveListener(OnRelease);
        }
    }
}