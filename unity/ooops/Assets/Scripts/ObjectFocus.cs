using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine;       
using UnityEngine.InputSystem;   

public class ObjectFocus : MonoBehaviour, IPointerClickHandler
{
    [Header("Cinemachine Cameras")]
    public CinemachineCamera puzzleCamera;
    public CinemachineCamera sharedFocusCamera;

    [Header("Framing Settings")]
    public Vector3 cameraLocalOffset = new Vector3(0f, 0.5f, -1.5f);

    private static bool isAnyObjectFocused = false;
    private static ObjectFocus currentlyFocusedObject = null;

    // 💡 NEW: We need to know if the game has actually started!
    private PuzzleLogic puzzleLogic;

    private void Start()
    {
        puzzleLogic = FindAnyObjectByType<PuzzleLogic>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 💡 BUG FIX: If the vase hasn't exploded yet, ignore the click completely!
        if (puzzleLogic != null && !puzzleLogic.hasExploded) return;

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

        if (sharedFocusCamera == null || puzzleCamera == null) return;

        isAnyObjectFocused = true;
        currentlyFocusedObject = this;

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

    private void Update()
    {
        if (!isAnyObjectFocused || currentlyFocusedObject != this) return;

        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            Vector2 screenPosition = Pointer.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(screenPosition);
            
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform != this.transform && !hit.transform.IsChildOf(this.transform))
                {
                    if (hit.transform.GetComponent<ObjectFocus>() == null)
                    {
                        ResetFocus();
                    }
                }
            }
            else
            {
                ResetFocus();
            }
        }
    }
}