using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class EnhancedLightController : MonoBehaviour
{
    [Header("Light Settings")]
    [Tooltip("Drag your Directional Light here")]
    [SerializeField] private Light globalLight;
    
    [Header("Brightness Range")]
    [SerializeField] private float minIntensity = 0f;
    [SerializeField] private float maxIntensity = 5f;
    
    [Header("Controller Settings")]
    [Tooltip("How fast brightness changes with joystick")]
    [SerializeField] private float joystickSpeed = 3f;
    
    [Header("Hand Slider Settings")]
    [Tooltip("Enable hand slider control")]
    [SerializeField] private bool enableHandSlider = true;
    
    [Tooltip("The slider object that hands can grab and move")]
    [SerializeField] private GameObject sliderKnob;
    
    [Tooltip("How far the slider can move vertically")]
    [SerializeField] private float sliderRange = 0.5f;
    
    [Header("Visual Feedback")]
    [Tooltip("Show brightness percentage in console")]
    [SerializeField] private bool showDebug = false;
    
    private float currentIntensity;
    private InputDevice leftController;
    private XRGrabInteractable sliderGrabInteractable;
    private Vector3 sliderStartPosition;
    private bool isSliderGrabbed = false;
    private float sliderMinY;
    private float sliderMaxY;

    void Start()
    {
        // Find or assign light
        if (globalLight == null)
        {
            globalLight = RenderSettings.sun;
            if (globalLight == null)
            {
                Debug.LogError("No global light found!");
                return;
            }
        }
        
        currentIntensity = globalLight.intensity;
        
        // Setup controller
        leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        
        // Setup hand slider if enabled
        if (enableHandSlider && sliderKnob != null)
        {
            SetupSlider();
        }
        
        if (showDebug)
            Debug.Log($"Light Controller initialized | Intensity: {currentIntensity:F1}");
    }

    void SetupSlider()
    {
        // Store original position
        sliderStartPosition = sliderKnob.transform.position;
        sliderMinY = sliderStartPosition.y;
        sliderMaxY = sliderStartPosition.y + sliderRange;
        
        // Add or get XRGrabInteractable
        sliderGrabInteractable = sliderKnob.GetComponent<XRGrabInteractable>();
        if (sliderGrabInteractable == null)
        {
            sliderGrabInteractable = sliderKnob.AddComponent<XRGrabInteractable>();
        }
        
        // Configure for slider behavior
        sliderGrabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
        sliderGrabInteractable.trackPosition = true;
        sliderGrabInteractable.trackRotation = false;
        
        // Add Rigidbody if needed
        Rigidbody rb = sliderKnob.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = sliderKnob.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
        
        // Add collider if needed
        if (sliderKnob.GetComponent<Collider>() == null)
        {
            BoxCollider col = sliderKnob.AddComponent<BoxCollider>();
            col.size = new Vector3(0.1f, 0.1f, 0.1f);
        }
        
        // Subscribe to events
        sliderGrabInteractable.selectEntered.AddListener(OnSliderGrabbed);
        sliderGrabInteractable.selectExited.AddListener(OnSliderReleased);
        
        if (showDebug)
            Debug.Log($"Slider setup complete | Range: {sliderMinY:F2} to {sliderMaxY:F2}");
    }

    void Update()
    {
        if (globalLight == null) return;
        
        // Controller joystick control
        UpdateControllerInput();
        
        // Hand slider control
        if (enableHandSlider && isSliderGrabbed)
        {
            UpdateSliderBrightness();
        }
        
        // Apply brightness
        globalLight.intensity = currentIntensity;
    }

    void UpdateControllerInput()
    {
        if (!leftController.isValid)
        {
            leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (!leftController.isValid) return;
        }
        
        Vector2 thumbstick;
        if (leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out thumbstick))
        {
            float joystickInput = thumbstick.y;
            
            if (Mathf.Abs(joystickInput) > 0.1f)
            {
                currentIntensity += joystickInput * joystickSpeed * Time.deltaTime;
                currentIntensity = Mathf.Clamp(currentIntensity, minIntensity, maxIntensity);
                
                if (showDebug && Time.frameCount % 30 == 0)
                {
                    float percentage = (currentIntensity - minIntensity) / (maxIntensity - minIntensity) * 100f;
                    Debug.Log($"Controller brightness: {percentage:F0}%");
                }
            }
        }
    }

    void UpdateSliderBrightness()
    {
        if (sliderKnob == null) return;
        
        // Get current Y position
        float currentY = sliderKnob.transform.position.y;
        
        // Constrain slider to vertical axis only
        Vector3 constrainedPos = sliderKnob.transform.position;
        constrainedPos.x = sliderStartPosition.x;
        constrainedPos.z = sliderStartPosition.z;
        constrainedPos.y = Mathf.Clamp(currentY, sliderMinY, sliderMaxY);
        sliderKnob.transform.position = constrainedPos;
        
        // Map slider position to brightness (0 to 1)
        float normalizedPosition = (constrainedPos.y - sliderMinY) / (sliderMaxY - sliderMinY);
        
        // Apply to light intensity
        currentIntensity = Mathf.Lerp(minIntensity, maxIntensity, normalizedPosition);
        
        if (showDebug && Time.frameCount % 30 == 0)
        {
            float percentage = normalizedPosition * 100f;
            Debug.Log($"Slider brightness: {percentage:F0}%");
        }
    }

    void OnSliderGrabbed(SelectEnterEventArgs args)
    {
        isSliderGrabbed = true;
        if (showDebug)
            Debug.Log("✓ Slider grabbed");
    }

    void OnSliderReleased(SelectExitEventArgs args)
    {
        isSliderGrabbed = false;
        if (showDebug)
            Debug.Log("✓ Slider released");
    }

    void OnDestroy()
    {
        if (sliderGrabInteractable != null)
        {
            sliderGrabInteractable.selectEntered.RemoveListener(OnSliderGrabbed);
            sliderGrabInteractable.selectExited.RemoveListener(OnSliderReleased);
        }
    }

    // Public method to set brightness from other scripts
    public void SetBrightness(float normalizedValue)
    {
        currentIntensity = Mathf.Lerp(minIntensity, maxIntensity, Mathf.Clamp01(normalizedValue));
    }

    // Get current brightness as percentage
    public float GetBrightnessPercentage()
    {
        return (currentIntensity - minIntensity) / (maxIntensity - minIntensity);
    }
}