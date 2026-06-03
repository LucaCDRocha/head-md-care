using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine;       // Required for Cinemachine v3
using UnityEngine.InputSystem;   // Required for modern Input System package

public class ObjectFocus : MonoBehaviour, IPointerClickHandler
{
    [Header("Cinemachine Cameras")]
    [Tooltip("The default primary camera view looking at your puzzle vase (e.g., 'CinemachineCamera').")]
    public CinemachineCamera puzzleCamera;
    
    [Tooltip("The shared virtual camera used for zooming into objects (e.g., 'FocusCamera').")]
    public CinemachineCamera sharedFocusCamera;

    [Header("Framing Settings")]
    [Tooltip("Pure coordinate unit offset relative to the object's local orientation. (Scale-Independent)")]
    public Vector3 cameraLocalOffset = new Vector3(0f, 0.5f, -1.5f);

    // Global static tracking so instances share room focus state
    private static bool isAnyObjectFocused = false;
    private static ObjectFocus currentlyFocusedObject = null;

    // This handles clicking ON the object (Works perfectly on mouse AND touch screens!)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isAnyObjectFocused && currentlyFocusedObject == this)
        {
            ResetFocus();
        }
        else
        {
            FocusOnThisObject();
        }
    }

    private void FocusOnThisObject()
    {
        if (puzzleCamera == null) puzzleCamera = GameObject.Find("CinemachineCamera")?.GetComponent<CinemachineCamera>();
        if (sharedFocusCamera == null) sharedFocusCamera = GameObject.Find("FocusCamera")?.GetComponent<CinemachineCamera>();

        if (sharedFocusCamera == null || puzzleCamera == null)
        {
            Debug.LogError($"ObjectFocus on {gameObject.name} is missing active camera references!");
            return;
        }

        isAnyObjectFocused = true;
        currentlyFocusedObject = this;

        // SCALE-INDEPENDENT MATH: Rotates the vector without multiplying by inspector scale metrics
        Vector3 rotatedOffset = transform.rotation * cameraLocalOffset;
        Vector3 targetPos = transform.position + rotatedOffset;
        Quaternion targetRot = Quaternion.LookRotation(transform.position - targetPos);

        sharedFocusCamera.transform.position = targetPos;
        sharedFocusCamera.transform.rotation = targetRot;

        sharedFocusCamera.Priority = 20;
        puzzleCamera.Priority = 10;
    }

    public void ResetFocus()
    {
        isAnyObjectFocused = false;
        currentlyFocusedObject = null;
        
        if (puzzleCamera == null) puzzleCamera = GameObject.Find("CinemachineCamera")?.GetComponent<CinemachineCamera>();
        if (sharedFocusCamera == null) sharedFocusCamera = GameObject.Find("FocusCamera")?.GetComponent<CinemachineCamera>();

        if (puzzleCamera != null) puzzleCamera.Priority = 20;
        if (sharedFocusCamera != null) sharedFocusCamera.Priority = 10;
    }

    // This handles clicking AWAY from the object on both PC and mobile touch devices
    private void Update()
    {
        if (!isAnyObjectFocused || currentlyFocusedObject != this) return;

        // Pointer.current handles BOTH Mouse clicks and Touchscreen presses automatically!
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            Vector2 screenPosition = Pointer.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(screenPosition);
            
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // If we clicked/tapped another object that is NOT this object or its children
                if (hit.transform != this.transform && !hit.transform.IsChildOf(this.transform))
                {
                    // If that object doesn't have its own zoom script, zoom back out
                    if (hit.transform.GetComponent<ObjectFocus>() == null)
                    {
                        ResetFocus();
                    }
                }
            }
            else
            {
                // Zoom out if player clicked completely empty space / skybox
                ResetFocus();
            }
        }
    }
}