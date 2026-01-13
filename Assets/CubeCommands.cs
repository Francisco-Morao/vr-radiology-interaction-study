using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class CubeYAxisRotation : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Rotation speed multiplier (higher = faster response)")]
    [SerializeField] private float rotationSpeed = 400f;
    
    [Tooltip("Hand rotation speed multiplier (separate from controllers)")]
    [SerializeField] private float handRotationSpeed = 300f;
    
    [Tooltip("Z-axis rotation speed multiplier for wrist twist")]
    [SerializeField] private float zRotationSpeed = 200f;
    
    [Tooltip("Smoothing for rotation (0 = instant, 0.9 = very smooth)")]
    [SerializeField] private float rotationSmoothing = 0.8f;
    
    [Tooltip("Hand smoothing (0 = instant, 0.9 = very smooth)")]
    [SerializeField] private float handRotationSmoothing = 0.6f;
    
    [Tooltip("Z-axis rotation smoothing")]
    [SerializeField] private float zRotationSmoothing = 0.7f;
    
    [Tooltip("Minimum controller velocity to trigger rotation (m/s)")]
    [SerializeField] private float velocityThreshold = 0.01f;
    
    [Tooltip("Minimum angular velocity to trigger Z rotation (degrees/s)")]
    [SerializeField] private float angularVelocityThreshold = 5f;
    
    [Header("Two-Handed Scaling")]
    [Tooltip("Enable scaling with both controllers")]
    [SerializeField] private bool enableTwoHandedScaling = true;
    
    [Tooltip("Minimum scale size")]
    [SerializeField] private float minScale = 0.2f;
    
    [Tooltip("Maximum scale size")]
    [SerializeField] private float maxScale = 5f;
    
    [Header("Reset Settings")]
    [Tooltip("Reset cube rotation when pressing left controller button")]
    [SerializeField] private bool enableReset = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = false;

    [SerializeField] private ScaleDisplayPanel scalePanel;

    [SerializeField] private TrialManager trialManager;

    [Header("Bingo Configuration 1")]
    [SerializeField] private Vector3 bingo1Rotation = Vector3.zero;
    [SerializeField] private Vector3 bingo1Scale = Vector3.one;

    [Header("Bingo Configuration 2")]
    [SerializeField] private Vector3 bingo2Rotation = Vector3.zero;
    [SerializeField] private Vector3 bingo2Scale = Vector3.one;

    private float bingoLenience = 25f;

    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    
    private InputDevice rightController;
    private InputDevice leftController;
    
    private Transform rightInteractorTransform;
    private Transform leftInteractorTransform;
    private Vector3 lastRightPosition;
    private Vector3 lastLeftPosition;
    
    // Track previous rotation for Z-axis
    private Quaternion lastRightRotation;
    private Quaternion lastLeftRotation;
    
    private bool isGrabbed = false;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private bool isTwoHandedGrab = false;
    private float initialGrabDistance = 0f;
    private Vector3 scaleAtGrabStart;
    private Vector3 currentScale;
    
    private float currentRotationVelocity = 0f;
    private float targetRotationVelocity = 0f;
    private float currentZRotationVelocity = 0f;
    
    // Separate smoothing accumulators for hands
    private float handCurrentRotationVelocity = 0f;
    private float handTargetRotationVelocity = 0f;
    
    private bool isUsingRightHand = false;
    private bool isUsingLeftHand = false;
    
    // Hand pinch reset tracking
    private float pinchStartTime = 0f;
    private bool isPinching = false;

    private Vector3 activeBingoRotation;
    private Vector3 activeBingoScale;
    private float activeBingoLenience;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        
        // Validate transform before starting
        ValidateTransform();
        
        grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
        grabInteractable.trackPosition = false;
        grabInteractable.trackRotation = false;
        grabInteractable.smoothPosition = false;
        grabInteractable.smoothRotation = false;
        
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    void Start()
    {
        originalRotation = transform.rotation;
        originalScale = transform.localScale;
        currentScale = originalScale;
        
        // Additional validation
        ValidateTransform();
        
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
        }
        
        rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        
        // UpdateActiveBingo();
    }

    public void UpdateActiveBingo()
    {
        // Use TrialData.currentBingoIndex to determine which bingo to use
        if (TrialData.currentBingoIndex == 0)
        {
            activeBingoRotation = bingo1Rotation;
            activeBingoScale = bingo1Scale;
        }
        else
        {
            activeBingoRotation = bingo2Rotation;
            activeBingoScale = bingo2Scale;
        }
        activeBingoLenience = bingoLenience;

    }

    private void ValidateTransform()
    {
        // Fix corrupt transforms
        if (float.IsNaN(transform.position.x) || float.IsInfinity(transform.position.x) ||
            float.IsNaN(transform.position.y) || float.IsInfinity(transform.position.y) ||
            float.IsNaN(transform.position.z) || float.IsInfinity(transform.position.z))
        {
            Debug.LogError("Invalid position detected! Resetting to origin.");
            transform.position = Vector3.zero;
        }

        if (float.IsNaN(transform.localScale.x) || float.IsInfinity(transform.localScale.x) ||
            transform.localScale.x == 0 || transform.localScale.x > 100f)
        {
            Debug.LogError("Invalid scale detected! Resetting to (1,1,1).");
            transform.localScale = Vector3.one;
        }

        if (float.IsNaN(transform.rotation.x) || float.IsInfinity(transform.rotation.x))
        {
            Debug.LogError("Invalid rotation detected! Resetting to identity.");
            transform.rotation = Quaternion.identity;
        }
    }

    void Update()
    {
        UpdateActiveBingo();
        CheckResetButton();
        
        if (enableTwoHandedScaling)
        {
            CheckTwoHandedScaling();
        }

        if (scalePanel != null && !isGrabbed && !isTwoHandedGrab)
            scalePanel.UpdateScaleDisplay();
        
        if (!isGrabbed || isTwoHandedGrab) return;
        
        if (isUsingRightHand && rightInteractorTransform != null)
        {
            ProcessHandRotation(rightInteractorTransform, ref lastRightPosition, ref lastRightRotation);
        }
        else if (!isUsingRightHand)
        {
            ProcessControllerRotation();
        }
    }

    private void CheckMatchedCubePosition()
    {
        if (trialManager == null) return;

        Vector3 bingoRotation = activeBingoRotation;
        Vector3 bingoScale = activeBingoScale;
        float rotationTolerance = activeBingoLenience; // degrees
        float scaleTolerance = 0.25f; // absolute units

        // === ROTATION CHECK ===
        // Convert target Euler angles to Quaternion
        Quaternion targetRotation = Quaternion.Euler(bingoRotation);
        Quaternion currentRotation = transform.rotation;
        
        // Calculate the angular difference between current and target rotation
        float angleDifference = Quaternion.Angle(currentRotation, targetRotation);
        
        // Debug: Show rotation comparison
        if (showDebugMessages && Time.frameCount % 30 == 0)
        {
            Debug.Log($"Rotation Check - Current: {currentRotation.eulerAngles}, Target: {bingoRotation}, Angle Diff: {angleDifference:F2}°, Tolerance: {rotationTolerance}°");
        }
        
        // Check if rotation is within tolerance
        bool rotationMatched = angleDifference <= rotationTolerance;
        
        if (!rotationMatched)
        {
            return; // Exit early if rotation doesn't match
        }
        
        Debug.Log($"✓ Cube matched target rotation! (Angle difference: {angleDifference:F2}°)");
        
        // === SCALE CHECK ===
        // Calculate scale error for each axis
        float scaleErrorX = Mathf.Abs(transform.localScale.x - bingoScale.x);
        float scaleErrorY = Mathf.Abs(transform.localScale.y - bingoScale.y);
        float scaleErrorZ = Mathf.Abs(transform.localScale.z - bingoScale.z);
        
        // Use average scale error (or you could use max if you want ALL axes within tolerance)
        float averageScaleError = (scaleErrorX + scaleErrorY + scaleErrorZ) / 3f;
        float maxScaleError = Mathf.Max(scaleErrorX, Mathf.Max(scaleErrorY, scaleErrorZ));
        
        // Debug: Always log scale info when rotation matches
        Debug.Log($"Scale Check - Current: {transform.localScale}, Target: {bingoScale}");
        Debug.Log($"Scale Errors - X: {scaleErrorX:F3}, Y: {scaleErrorY:F3}, Z: {scaleErrorZ:F3}");
        Debug.Log($"Average Error: {averageScaleError:F3}, Max Error: {maxScaleError:F3}, Tolerance: {scaleTolerance}");
        
        // Check if scale is within tolerance (using max error - all axes must be close)
        bool scaleMatched = maxScaleError <= scaleTolerance;
        
        if (!scaleMatched)
        {
            Debug.LogWarning($"✗ Scale not matched! Max error: {maxScaleError:F3} > Tolerance: {scaleTolerance}");
            return;
        }
        
        Debug.Log($"✓ Scale matched! (Max error: {maxScaleError:F3})");
        
        // === CALCULATE ERROR PERCENTAGES ===
        // For rotation: convert angle difference to percentage of tolerance
        float rotationErrorPercent = (angleDifference / rotationTolerance) * 100f;
        
        // For scale: convert max error to percentage of tolerance
        float scaleErrorPercent = (maxScaleError / scaleTolerance) * 100f;
        
        // Store errors
        // If you want to keep tracking X, Y, Z rotation separately, you'll need to recalculate
        // For now, using the overall angle difference for all rotation axes
        TrialData.errors[0] = Mathf.RoundToInt(rotationErrorPercent); // X rotation (using overall angle)
        TrialData.errors[1] = Mathf.RoundToInt(rotationErrorPercent); // Y rotation (using overall angle)
        TrialData.errors[2] = Mathf.RoundToInt(rotationErrorPercent); // Z rotation (using overall angle)
        TrialData.errors[3] = Mathf.RoundToInt(scaleErrorPercent);     // Scale error
        
        // Calculate total error as AVERAGE
        TrialData.errors[4] = Mathf.RoundToInt(
            (rotationErrorPercent + scaleErrorPercent) / 2f
        );
        
        if (showDebugMessages)
        {
            Debug.Log($"Error Percentages - Rotation: {rotationErrorPercent:F1}% | Scale: {scaleErrorPercent:F1}% | Average: {TrialData.errors[4]}%");
        }
        
        // === SUCCESS! ===
        trialManager.FinishScene();
        Debug.Log("BINGOOOOOOOOOO");
    }


    
    private void ProcessHandRotation(Transform handTransform, ref Vector3 lastPosition, ref Quaternion lastRotation)
    {
        // Validate hand transform
        if (handTransform == null || !handTransform.gameObject.activeInHierarchy)
        {
            if (showDebugMessages && Time.frameCount % 60 == 0)
                Debug.LogWarning("ProcessHandRotation: handTransform is null or inactive");
            return;
        }

        // Find the actual hand root (wrist or palm) to track movement
        Transform handRoot = FindHandRoot(handTransform);
        if (handRoot == null)
        {
            if (showDebugMessages && Time.frameCount % 60 == 0)
                Debug.LogWarning($"ProcessHandRotation: Could not find hand root for {handTransform.name}, trying parent directly");
            
            handRoot = handTransform.parent;
            if (handRoot == null)
            {
                if (showDebugMessages)
                    Debug.LogError("ProcessHandRotation: No parent found either!");
                return;
            }
        }

        Vector3 currentPosition = handRoot.position;
        Quaternion currentRotation = handRoot.rotation;
        
        // Validate position
        if (float.IsNaN(currentPosition.x) || float.IsInfinity(currentPosition.x))
        {
            if (showDebugMessages)
                Debug.LogWarning("Invalid position detected");
            return;
        }
        
        // Initialize last position/rotation on first frame
        if (lastPosition == Vector3.zero)
        {
            lastPosition = currentPosition;
            lastRotation = currentRotation;
            return;
        }
        
        // === POSITIONAL ROTATION (X and Y axes) ===
        Vector3 deltaPosition = currentPosition - lastPosition;
        Vector3 velocity = deltaPosition / Time.deltaTime;
        velocity = Vector3.ClampMagnitude(velocity, 10f);
        
        Vector3 cameraForward = Camera.main.transform.forward;
        Vector3 cameraRight = Camera.main.transform.right;
        
        float xVelocity = Vector3.Dot(velocity, cameraRight);
        float zVelocity = Vector3.Dot(velocity, cameraForward);
        
        float yAxisRotationVelocity = xVelocity * handRotationSpeed;
        float xAxisRotationVelocity = -zVelocity * handRotationSpeed;
        
        float smoothedYRotation = Mathf.Lerp(handCurrentRotationVelocity, yAxisRotationVelocity, 1f - handRotationSmoothing);
        float smoothedXRotation = Mathf.Lerp(handTargetRotationVelocity, xAxisRotationVelocity, 1f - handRotationSmoothing);
        
        handCurrentRotationVelocity = smoothedYRotation;
        handTargetRotationVelocity = smoothedXRotation;
        
        currentRotationVelocity = smoothedYRotation;
        targetRotationVelocity = smoothedXRotation;
        
        float yRotationThisFrame = smoothedYRotation * Time.deltaTime;
        float xRotationThisFrame = smoothedXRotation * Time.deltaTime;
        
        yRotationThisFrame = Mathf.Clamp(yRotationThisFrame, -90f, 90f);
        xRotationThisFrame = Mathf.Clamp(xRotationThisFrame, -90f, 90f);
        
        // === ROTATIONAL TWIST (Z axis) ===
        // Calculate the angular difference around the forward axis (wrist twist)
        Quaternion deltaRotation = currentRotation * Quaternion.Inverse(lastRotation);
        
        // Extract the twist angle around the hand's forward axis (Z-axis in hand space)
        Vector3 handForward = handRoot.forward;
        Vector3 deltaAxis;
        float deltaAngle;
        deltaRotation.ToAngleAxis(out deltaAngle, out deltaAxis);
        
        // Normalize angle to -180 to 180 range
        if (deltaAngle > 180f)
            deltaAngle -= 360f;
        
        // Project rotation onto the hand's forward axis to get twist
        float twistAngle = Vector3.Dot(deltaAxis.normalized, handForward) * deltaAngle;
        
        // Convert to angular velocity (degrees per second)
        float angularVelocity = twistAngle / Time.deltaTime;
        
        // Apply Z-rotation speed multiplier
        float zAxisRotationVelocity = angularVelocity * zRotationSpeed;
        
        // Smooth the Z rotation
        float smoothedZRotation = Mathf.Lerp(currentZRotationVelocity, zAxisRotationVelocity, 1f - zRotationSmoothing);
        currentZRotationVelocity = smoothedZRotation;
        
        float zRotationThisFrame = smoothedZRotation * Time.deltaTime;
        zRotationThisFrame = Mathf.Clamp(zRotationThisFrame, -90f, 90f);
        
        // === APPLY ALL ROTATIONS ===
        float handThreshold = velocityThreshold * 0.5f;
        bool isRotating = false;
        
        // Apply X and Y rotation from position movement
        if (Mathf.Abs(smoothedYRotation) > handThreshold || Mathf.Abs(smoothedXRotation) > handThreshold)
        {
            transform.Rotate(-xRotationThisFrame, -yRotationThisFrame, 0f, Space.World);
            isRotating = true;
        }
        
        // Apply Z rotation from wrist twist
        if (Mathf.Abs(smoothedZRotation) > angularVelocityThreshold)
        {
            // Get camera forward as the Z-axis reference
            Vector3 cameraForwardAxis = Camera.main.transform.forward;
            transform.Rotate(cameraForwardAxis, zRotationThisFrame, Space.World);
            isRotating = true;
        }
        
        if (isRotating)
        {
            ValidateTransform();
        }
        
        // Update last values
        lastPosition = currentPosition;
        lastRotation = currentRotation;
        
        if (showDebugMessages && Time.frameCount % 30 == 0)
        {
            Debug.Log($"HAND ROTATION | Pos Vel X={xVelocity:F2} Z={zVelocity:F2} | Twist={twistAngle:F1}° | " +
                     $"Smoothed Y={smoothedYRotation:F1} X={smoothedXRotation:F1} Z={smoothedZRotation:F1} | Rotating={isRotating}");
        }
    }
    
    private Transform FindHandRoot(Transform interactorTransform)
    {
        Transform current = interactorTransform;
        Transform handParent = null;
        int safety = 0;
        
        if (showDebugMessages && Time.frameCount % 120 == 0)
            Debug.Log($"FindHandRoot: Starting search from {interactorTransform.name}");
        
        while (current != null && safety < 15)
        {
            string name = current.name.ToLower();
            
            if ((name.Contains("right hand") || name.Contains("left hand")) && 
                !name.Contains("interactor") && 
                !name.Contains("visual") &&
                !name.Contains("tracking"))
            {
                handParent = current;
                if (showDebugMessages && Time.frameCount % 120 == 0)
                    Debug.Log($"  Found hand parent: {current.name}");
                break;
            }
            
            current = current.parent;
            safety++;
        }
        
        if (handParent == null)
        {
            if (showDebugMessages && Time.frameCount % 120 == 0)
                Debug.LogWarning($"  ✗ Could not find hand parent");
            return null;
        }
        
        Transform[] children = handParent.GetComponentsInChildren<Transform>();
        
        foreach (Transform child in children)
        {
            string childName = child.name.ToLower();
            
            if (childName.Contains("aim pose") || 
                childName.Contains("pinch point") || 
                childName.Contains("pinch grab") ||
                childName.Contains("wrist") || 
                childName.Contains("palm") ||
                childName.Contains("stabilized"))
            {
                if (child.position != Vector3.zero)
                {
                    if (showDebugMessages && Time.frameCount % 120 == 0)
                        Debug.Log($"  ✓ Found tracking transform: {child.name} at position {child.position}");
                    return child;
                }
            }
        }
        
        if (showDebugMessages && Time.frameCount % 120 == 0)
            Debug.LogWarning($"  ⚠ No tracking child found, using hand parent: {handParent.name}");
        
        return handParent;
    }
    
    private void ProcessControllerRotation()
    {
        if (!rightController.isValid)
        {
            rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!rightController.isValid) return;
        }
        
        // === POSITIONAL ROTATION (X and Y axes) ===
        Vector3 controllerVelocity;
        if (rightController.TryGetFeatureValue(CommonUsages.deviceVelocity, out controllerVelocity))
        {
            Quaternion controllerRotation;
            if (!rightController.TryGetFeatureValue(CommonUsages.deviceRotation, out controllerRotation))
            {
                return;
            }
            
            Vector3 localVelocity = Quaternion.Inverse(controllerRotation) * controllerVelocity;
            
            float xVelocity = localVelocity.x;
            float zVelocity = localVelocity.z;
            
            float yAxisRotationVelocity = xVelocity * rotationSpeed;
            float xAxisRotationVelocity = -zVelocity * rotationSpeed;
            
            float smoothedYRotation = Mathf.Lerp(currentRotationVelocity, yAxisRotationVelocity, 1f - rotationSmoothing);
            float smoothedXRotation = Mathf.Lerp(targetRotationVelocity, xAxisRotationVelocity, 1f - rotationSmoothing);
            
            currentRotationVelocity = smoothedYRotation;
            targetRotationVelocity = smoothedXRotation;
            
            float yRotationThisFrame = smoothedYRotation * Time.deltaTime;
            float xRotationThisFrame = smoothedXRotation * Time.deltaTime;
            
            if (Mathf.Abs(smoothedYRotation) > velocityThreshold || Mathf.Abs(smoothedXRotation) > velocityThreshold)
            {
                transform.Rotate(-xRotationThisFrame, -yRotationThisFrame, 0f, Space.World);
            }
            
            // === ANGULAR ROTATION (Z axis from controller twist) ===
            Vector3 angularVelocity;
            if (rightController.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out angularVelocity))
            {
                // Transform angular velocity to local controller space
                Vector3 localAngularVelocity = Quaternion.Inverse(controllerRotation) * angularVelocity;
                
                // The Z component represents twist around the controller's forward axis
                float twistVelocity = localAngularVelocity.z * Mathf.Rad2Deg; // Convert to degrees
                
                float zAxisRotationVelocity = twistVelocity * zRotationSpeed;
                
                float smoothedZRotation = Mathf.Lerp(currentZRotationVelocity, zAxisRotationVelocity, 1f - zRotationSmoothing);
                currentZRotationVelocity = smoothedZRotation;
                
                float zRotationThisFrame = smoothedZRotation * Time.deltaTime;
                
                if (Mathf.Abs(smoothedZRotation) > angularVelocityThreshold)
                {
                    // Use camera forward as Z-axis
                    Vector3 cameraForwardAxis = Camera.main.transform.forward;
                    transform.Rotate(cameraForwardAxis, zRotationThisFrame, Space.World);
                }
                
                if (showDebugMessages && Time.frameCount % 30 == 0)
                {
                    Debug.Log($"CONTROLLER | Pos X={xVelocity:F4} Z={zVelocity:F4} | Twist={twistVelocity:F1}°/s | " +
                             $"Y-Rot={yRotationThisFrame:F2}° X-Rot={xRotationThisFrame:F2}° Z-Rot={zRotationThisFrame:F2}°");
                }
            }
            else if (showDebugMessages && Time.frameCount % 10 == 0)
            {
                Debug.Log($"CONTROLLER | X={xVelocity:F4} Z={zVelocity:F4} | Y-Rot={yRotationThisFrame:F2}° X-Rot={xRotationThisFrame:F2}°");
            }
        }
    }
    
    void LateUpdate()
    {
        if (transform.localScale != currentScale)
        {
            transform.localScale = currentScale;
        }
        
        if (Time.frameCount % 60 == 0)
        {
            ValidateTransform();
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        var interactor = args.interactorObject;
        
        if (interactor == null)
        {
            Debug.LogWarning("Null interactor in OnGrabbed");
            return;
        }

        TrialData.manipulationCount++;
        
        Transform interactorTransform = interactor.transform;
        string fullPath = GetFullPath(interactorTransform).ToLower();
        
        bool isRightSide = fullPath.Contains("right");
        bool isLeftSide = fullPath.Contains("left");
        
        bool isHand = (fullPath.Contains("handquest") || 
                       fullPath.Contains("hand visual") ||
                       fullPath.Contains("hand poke") ||
                       fullPath.Contains("hand near") ||
                       fullPath.Contains("wrist") || 
                       fullPath.Contains("palm") ||
                       (fullPath.Contains("hand") && !fullPath.Contains("controller")));
        
        if (isRightSide)
        {
            isGrabbed = true;
            rightInteractorTransform = interactorTransform;
            isUsingRightHand = isHand;
            lastRightPosition = Vector3.zero;
            lastRightRotation = Quaternion.identity;
            currentRotationVelocity = 0f;
            targetRotationVelocity = 0f;
            currentZRotationVelocity = 0f;
            handCurrentRotationVelocity = 0f;
            handTargetRotationVelocity = 0f;
            
            CheckIfTwoHandedGrab();
            
            if (showDebugMessages)
            {
                Debug.Log($"✓ RIGHT {(isHand ? "HAND" : "CONTROLLER")} grabbed");
            }
        }
        else if (isLeftSide && isGrabbed)
        {
            isTwoHandedGrab = true;
            leftInteractorTransform = interactorTransform;
            isUsingLeftHand = isHand;
            lastLeftPosition = Vector3.zero;
            lastLeftRotation = Quaternion.identity;
            
            InitializeTwoHandedGrab();
            
            if (showDebugMessages)
            {
                Debug.Log($"✓ LEFT {(isHand ? "HAND" : "CONTROLLER")} joined - TWO-HANDED MODE");
            }
        }
        else if (isLeftSide && !isGrabbed)
        {
            if (showDebugMessages)
            {
                Debug.Log("✗ Left tried to grab alone - must grab with RIGHT first");
            }
            grabInteractable.interactionManager.CancelInteractorSelection((IXRSelectInteractor)interactor);
        }
    }
    
    private string GetFullPath(Transform t)
    {
        if (t == null) return "null";
        
        string path = t.name;
        Transform current = t;
        int safety = 0;
        
        while (current.parent != null && safety < 20)
        {
            current = current.parent;
            path = current.name + "/" + path;
            safety++;
        }
        return path;
    }
    
    private void CheckResetButton()
    {
        if (!enableReset) return;
        
        if (!leftController.isValid)
        {
            leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (!leftController.isValid) return;
        }
        
        bool primaryButtonPressed;
        if (leftController.TryGetFeatureValue(CommonUsages.primaryButton, out primaryButtonPressed))
        {
            if (primaryButtonPressed)
            {
                ResetRotation();
            }
        }
    }
    
    private bool IsHandPinching(XRNode handNode)
    {
        InputDevice hand = InputDevices.GetDeviceAtXRNode(handNode);
        
        if (!hand.isValid) return false;
        
        float gripValue;
        if (hand.TryGetFeatureValue(CommonUsages.grip, out gripValue))
        {
            if (gripValue > 0.8f)
            {
                if (showDebugMessages && Time.frameCount % 30 == 0)
                    Debug.Log($"{handNode} grip: {gripValue:F2}");
                return true;
            }
        }
        
        float triggerValue;
        if (hand.TryGetFeatureValue(CommonUsages.trigger, out triggerValue))
        {
            if (triggerValue > 0.8f)
            {
                if (showDebugMessages && Time.frameCount % 30 == 0)
                    Debug.Log($"{handNode} trigger: {triggerValue:F2}");
                return true;
            }
        }
        
        bool pinchButton;
        if (hand.TryGetFeatureValue(CommonUsages.primaryButton, out pinchButton))
        {
            if (pinchButton)
            {
                if (showDebugMessages && Time.frameCount % 30 == 0)
                    Debug.Log($"{handNode} button pinched");
                return true;
            }
        }
        
        return false;
    }
    
    private void ResetRotation()
    {
        transform.rotation = originalRotation;
        transform.localScale = originalScale;
        currentScale = originalScale;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        ValidateTransform();
        
        if (showDebugMessages)
        {
            Debug.Log("⟲ Cube RESET to original rotation and scale");
        }
    }
    
    private void CheckIfTwoHandedGrab()
    {
        if (grabInteractable.interactorsSelecting.Count >= 2)
        {
            isTwoHandedGrab = true;
            InitializeTwoHandedGrab();
        }
    }
    
    private void InitializeTwoHandedGrab()
    {
        if (rightInteractorTransform == null || leftInteractorTransform == null)
            return;
        
        Transform rightRoot = isUsingRightHand ? FindHandRoot(rightInteractorTransform) : rightInteractorTransform;
        Transform leftRoot = isUsingLeftHand ? FindHandRoot(leftInteractorTransform) : leftInteractorTransform;
        
        if (rightRoot == null) rightRoot = rightInteractorTransform;
        if (leftRoot == null) leftRoot = leftInteractorTransform;
        
        Vector3 rightPos = rightRoot.position;
        Vector3 leftPos = leftRoot.position;
        
        initialGrabDistance = Vector3.Distance(rightPos, leftPos);
        
        if (float.IsNaN(initialGrabDistance) || initialGrabDistance < 0.01f || initialGrabDistance > 10f)
        {
            Debug.LogWarning($"Invalid grab distance: {initialGrabDistance:F2}m, using default 0.3m");
            initialGrabDistance = 0.3f;
        }
        
        scaleAtGrabStart = currentScale;

        if (rb != null)
            rb.isKinematic = true;

        if (showDebugMessages)
        {
            Debug.Log($"TWO-HANDED SCALING | Initial distance: {initialGrabDistance:F2}m | Starting scale: {scaleAtGrabStart.x:F2}");
        }
    }

    private void CheckTwoHandedScaling()
    {
        if (!isTwoHandedGrab || rightInteractorTransform == null || leftInteractorTransform == null)
            return;
        
        Transform rightRoot = isUsingRightHand ? FindHandRoot(rightInteractorTransform) : rightInteractorTransform;
        Transform leftRoot = isUsingLeftHand ? FindHandRoot(leftInteractorTransform) : leftInteractorTransform;
        
        if (rightRoot == null) rightRoot = rightInteractorTransform;
        if (leftRoot == null) leftRoot = leftInteractorTransform;
        
        Vector3 rightPos = rightRoot.position;
        Vector3 leftPos = leftRoot.position;
        
        float currentDistance = Vector3.Distance(rightPos, leftPos);
        
        if (float.IsNaN(currentDistance) || currentDistance < 0.01f)
            return;
            
        float scaleFactor = currentDistance / initialGrabDistance;
        scaleFactor = Mathf.Clamp(scaleFactor, 0.1f, 10f);
        
        Vector3 newScale = scaleAtGrabStart * scaleFactor;
        
        newScale.x = Mathf.Clamp(newScale.x, minScale, maxScale);
        newScale.y = Mathf.Clamp(newScale.y, minScale, maxScale);
        newScale.z = Mathf.Clamp(newScale.z, minScale, maxScale);
        
        currentScale = newScale;
        transform.localScale = newScale;
        
        if (showDebugMessages && Time.frameCount % 30 == 0)
        {
            Debug.Log($"SCALING | Distance: {currentDistance:F2}m | Factor: {scaleFactor:F2}x | Scale: {newScale.x:F2}");
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        var interactor = args.interactorObject;
        
        if (interactor == null) return;
        
        string fullPath = GetFullPath(interactor.transform).ToLower();
        
        bool isRightSide = fullPath.Contains("right");
        bool isLeftSide = fullPath.Contains("left");
        
        if (showDebugMessages)
        {
            string side = isRightSide ? "RIGHT" : "LEFT";
            Debug.Log($"✓ {side} released | Current scale: {currentScale.x:F2}");
        }
        
        if (isRightSide)
        {
            isGrabbed = false;
            isTwoHandedGrab = false;
            rightInteractorTransform = null;
            isUsingRightHand = false;
            lastRightPosition = Vector3.zero;
            lastRightRotation = Quaternion.identity;
        }
        else if (isLeftSide && isTwoHandedGrab)
        {
            isTwoHandedGrab = false;
            leftInteractorTransform = null;
            isUsingLeftHand = false;
            lastLeftPosition = Vector3.zero;
            lastLeftRotation = Quaternion.identity;
        }

        CheckMatchedCubePosition();
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }
}