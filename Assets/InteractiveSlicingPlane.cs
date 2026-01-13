using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Attach this to EACH SlicingPlane object
/// Allows left hand/controller to grab and move the slice
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class InteractiveSlicingPlane : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Which axis the slice moves along (X, Y, or Z)")]
    [SerializeField] private SliceAxis movementAxis = SliceAxis.X;
    
    [Tooltip("Minimum position on the axis (local space)")]
    [SerializeField] private float minPosition = -0.5f;
    
    [Tooltip("Maximum position on the axis (local space)")]
    [SerializeField] private float maxPosition = 0.5f;
    
    [Tooltip("Movement speed multiplier")]
    [SerializeField] private float movementSpeed = 1.5f;
    
    [Tooltip("Keep plane inside volume bounds")]
    [SerializeField] private bool constrainToVolume = true;
    
    [Header("Restrictions")]
    [Tooltip("Only allow left hand/controller to grab")]
    [SerializeField] private bool leftHandOnly = true;
    
    [Tooltip("Lock rotation when grabbed")]
    [SerializeField] private bool lockRotation = true;
    
    [Header("Visual Feedback")]
    [Tooltip("Highlight slice when grabbed")]
    [SerializeField] private bool highlightWhenGrabbed = true;
    
    [Tooltip("Color when grabbed")]
    [SerializeField] private Color grabbedColor = new Color(0.3f, 1f, 0.3f, 0.7f);
    
    [Tooltip("Color when not grabbed")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.5f);
    
    [Tooltip("Show slice plane gizmo in scene view")]
    [SerializeField] private bool showGizmo = true;
    
    [Header("Debug")]
    [SerializeField] private bool showDebug = false;
    
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Transform grabberTransform;
    private Vector3 lastGrabberPosition;
    private bool isGrabbed = false;
    private Renderer sliceRenderer;
    private Material sliceMaterial;
    private bool isUsingHand = false;
    private Color originalColor;
    
    public enum SliceAxis { X, Y, Z }

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        
        // Configure for slice behavior
        grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
        grabInteractable.trackPosition = false;
        grabInteractable.trackRotation = false;
        grabInteractable.smoothPosition = false;
        grabInteractable.smoothRotation = false;
        
        // Setup rigidbody
        rb.isKinematic = true;
        rb.useGravity = false;
        
        // Setup large collider for easy grabbing
        SetupGrabCollider();
        
        // Get renderer for visual feedback
        sliceRenderer = GetComponent<Renderer>();
        if (sliceRenderer != null && sliceRenderer.material != null)
        {
            sliceMaterial = sliceRenderer.material;
            
            // Check if material has color property before using it
            if (sliceMaterial.HasProperty("_Color"))
            {
                originalColor = sliceMaterial.color;
                sliceMaterial.color = normalColor;
            }
            else
            {
                // Shader doesn't support color changes - disable highlighting
                highlightWhenGrabbed = false;
                if (showDebug)
                    Debug.Log($"Material '{sliceMaterial.name}' doesn't support color changes - visual feedback disabled");
            }
        }
        
        // Subscribe to grab events
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
        
        // Store original transform
        originalPosition = transform.localPosition;
        originalRotation = transform.localRotation;
    }
    
    void SetupGrabCollider()
    {
        // Remove existing colliders
        Collider[] existingColliders = GetComponents<Collider>();
        foreach (Collider col in existingColliders)
        {
            if (Application.isPlaying)
                Destroy(col);
            else
                DestroyImmediate(col);
        }
        
        // Add a large box collider that covers the entire slice plane
        BoxCollider boxCol = gameObject.AddComponent<BoxCollider>();
        
        // Make it cover a large area (adjust based on your volume size)
        // This creates a thin, wide collider like a plane
        boxCol.size = new Vector3(2f, 2f, 0.1f); // Width, Height, Thickness
        boxCol.center = Vector3.zero;
        boxCol.isTrigger = false;
        
        if (showDebug)
            Debug.Log($"Setup large grab collider: {boxCol.size}");
    }

    void Start()
    {
        if (showDebug)
        {
            Debug.Log($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Debug.Log($"Slice Plane: {gameObject.name}");
            Debug.Log($"  Movement Axis: {movementAxis}");
            Debug.Log($"  Range: {minPosition:F2} to {maxPosition:F2}");
            Debug.Log($"  Local Position: {transform.localPosition}");
            Debug.Log($"  World Position: {transform.position}");
            Debug.Log($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }
    }

    void Update()
    {
        if (!isGrabbed) return;
        
        // Lock rotation if enabled
        if (lockRotation)
        {
            transform.localRotation = originalRotation;
        }
        
        // Process movement based on grabber type
        if (isUsingHand && grabberTransform != null)
        {
            ProcessHandMovement();
        }
        else if (!isUsingHand && grabberTransform != null)
        {
            ProcessControllerMovement();
        }
    }

    void ProcessHandMovement()
    {
        // Find the actual hand tracking point
        Transform handRoot = FindHandRoot(grabberTransform);
        if (handRoot == null) handRoot = grabberTransform;
        
        Vector3 currentPosition = handRoot.position;
        
        if (lastGrabberPosition == Vector3.zero)
        {
            lastGrabberPosition = currentPosition;
            return;
        }
        
        // Calculate hand movement delta
        Vector3 delta = currentPosition - lastGrabberPosition;
        
        // Apply movement on specified axis
        MoveSliceOnAxis(delta);
        
        lastGrabberPosition = currentPosition;
    }

    void ProcessControllerMovement()
    {
        Vector3 currentPosition = grabberTransform.position;
        
        if (lastGrabberPosition == Vector3.zero)
        {
            lastGrabberPosition = currentPosition;
            return;
        }
        
        // Calculate controller movement delta
        Vector3 delta = currentPosition - lastGrabberPosition;
        
        // Apply movement on specified axis
        MoveSliceOnAxis(delta);
        
        lastGrabberPosition = currentPosition;
    }

    void MoveSliceOnAxis(Vector3 worldDelta)
    {
        // Convert world delta to local space
        Vector3 localDelta = transform.parent != null 
            ? transform.parent.InverseTransformDirection(worldDelta) 
            : worldDelta;
        
        // Apply movement speed
        localDelta *= movementSpeed;
        
        Vector3 newLocalPos = transform.localPosition;
        float oldValue = 0f;
        float newValue = 0f;
        
        // Apply movement only on specified axis
        switch (movementAxis)
        {
            case SliceAxis.X:
                oldValue = newLocalPos.x;
                newLocalPos.x += localDelta.x;
                newLocalPos.x = Mathf.Clamp(newLocalPos.x, minPosition, maxPosition);
                newValue = newLocalPos.x;
                break;
            case SliceAxis.Y:
                oldValue = newLocalPos.y;
                newLocalPos.y += localDelta.y;
                newLocalPos.y = Mathf.Clamp(newLocalPos.y, minPosition, maxPosition);
                newValue = newLocalPos.y;
                break;
            case SliceAxis.Z:
                oldValue = newLocalPos.z;
                newLocalPos.z += localDelta.z;
                newLocalPos.z = Mathf.Clamp(newLocalPos.z, minPosition, maxPosition);
                newValue = newLocalPos.z;
                break;
        }
        
        // Only update if value changed
        if (Mathf.Abs(newValue - oldValue) > 0.001f)
        {
            transform.localPosition = newLocalPos;
            
            if (showDebug && Time.frameCount % 30 == 0)
            {
                float percentage = Mathf.InverseLerp(minPosition, maxPosition, newValue) * 100f;
                Debug.Log($"Slice {movementAxis}: {newValue:F3} ({percentage:F0}%) | World: {transform.position}");
            }
        }
    }

    float GetAxisPosition()
    {
        switch (movementAxis)
        {
            case SliceAxis.X: return transform.localPosition.x;
            case SliceAxis.Y: return transform.localPosition.y;
            case SliceAxis.Z: return transform.localPosition.z;
            default: return 0f;
        }
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        var interactor = args.interactorObject;
        Transform interactorTransform = interactor.transform;
        string fullPath = GetFullPath(interactorTransform).ToLower();
        
        // Check if it's left side
        bool isLeftSide = fullPath.Contains("left");
        
        if (leftHandOnly && !isLeftSide)
        {
            if (showDebug)
                Debug.Log($"⛔ Right hand tried to grab - only LEFT allowed");
            
            grabInteractable.interactionManager.CancelInteractorSelection((IXRSelectInteractor)interactor);
            return;
        }
        
        // Detect if hand or controller
        isUsingHand = (fullPath.Contains("handquest") || 
                       fullPath.Contains("hand visual") ||
                       fullPath.Contains("hand poke") ||
                       fullPath.Contains("hand near") ||
                       fullPath.Contains("wrist") || 
                       fullPath.Contains("palm") ||
                       (fullPath.Contains("hand") && !fullPath.Contains("controller")));
        
        isGrabbed = true;
        grabberTransform = interactorTransform;
        lastGrabberPosition = Vector3.zero;
        
        // Visual feedback
        if (highlightWhenGrabbed && sliceMaterial != null)
        {
            sliceMaterial.color = grabbedColor;
        }
        
        if (showDebug)
        {
            string grabberType = isUsingHand ? "HAND" : "CONTROLLER";
            string side = isLeftSide ? "LEFT" : "RIGHT";
            Debug.Log($"✓ Slice '{gameObject.name}' grabbed by {side} {grabberType}");
        }
    }

    void OnReleased(SelectExitEventArgs args)
    {
        isGrabbed = false;
        grabberTransform = null;
        lastGrabberPosition = Vector3.zero;
        
        // Visual feedback
        if (highlightWhenGrabbed && sliceMaterial != null)
        {
            sliceMaterial.color = normalColor;
        }
        
        if (showDebug)
        {
            float currentValue = GetAxisPosition();
            float percentage = Mathf.InverseLerp(minPosition, maxPosition, currentValue) * 100f;
            Debug.Log($"✓ Slice '{gameObject.name}' released | {movementAxis}={currentValue:F3} ({percentage:F0}%)");
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

    private Transform FindHandRoot(Transform interactorTransform)
    {
        Transform current = interactorTransform;
        Transform handParent = null;
        int safety = 0;
        
        // Find hand parent
        while (current != null && safety < 15)
        {
            string name = current.name.ToLower();
            
            if ((name.Contains("right hand") || name.Contains("left hand")) && 
                !name.Contains("interactor") && 
                !name.Contains("visual") &&
                !name.Contains("tracking"))
            {
                handParent = current;
                break;
            }
            
            current = current.parent;
            safety++;
        }
        
        if (handParent == null) return null;
        
        // Look for tracking transforms
        Transform[] children = handParent.GetComponentsInChildren<Transform>();
        
        foreach (Transform child in children)
        {
            string childName = child.name.ToLower();
            
            if (childName.Contains("aim pose") || 
                childName.Contains("pinch point") || 
                childName.Contains("wrist") || 
                childName.Contains("palm") ||
                childName.Contains("stabilized"))
            {
                if (child.position != Vector3.zero)
                {
                    return child;
                }
            }
        }
        
        return handParent;
    }

    // Public method to reset slice position
    public void ResetPosition()
    {
        transform.localPosition = originalPosition;
        transform.localRotation = originalRotation;
        
        if (showDebug)
            Debug.Log($"🔄 Slice '{gameObject.name}' reset to original position");
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }
    
    // Draw gizmo in scene view
    void OnDrawGizmos()
    {
        if (!showGizmo) return;
        
        Gizmos.color = isGrabbed ? Color.green : Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        
        // Draw the slice plane
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(2f, 2f, 0.02f));
        
        // Draw axis indicator
        Gizmos.color = Color.red;
        switch (movementAxis)
        {
            case SliceAxis.X:
                Gizmos.DrawLine(Vector3.zero, Vector3.right * 0.3f);
                break;
            case SliceAxis.Y:
                Gizmos.DrawLine(Vector3.zero, Vector3.up * 0.3f);
                break;
            case SliceAxis.Z:
                Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.3f);
                break;
        }
    }
}