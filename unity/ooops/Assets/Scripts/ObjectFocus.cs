using System.Collections;
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

    private static bool isTransitioning = false;
    private PuzzleLogic puzzleLogic;

    private void Start()
    {
        puzzleLogic = FindAnyObjectByType<PuzzleLogic>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (puzzleLogic != null && !puzzleLogic.hasExploded) return;

        // Ignore clicks if the camera is currently flying!
        if (isTransitioning) return;

        if (isAnyObjectFocused && currentlyFocusedObject == this)
        {
            StartCoroutine(TransitionRoutine(false));
        }
        else if (!isAnyObjectFocused)
        {
            StartCoroutine(TransitionRoutine(true));
        }
    }

    private IEnumerator TransitionRoutine(bool isFocusing)
    {
        isTransitioning = true; // 🔒 LOCK the camera

        if (isFocusing)
        {
            if (puzzleCamera == null) puzzleCamera = GameObject.Find("CinemachineCamera")?.GetComponent<CinemachineCamera>();
            if (sharedFocusCamera == null) sharedFocusCamera = GameObject.Find("FocusCamera")?.GetComponent<CinemachineCamera>();

            if (sharedFocusCamera != null && puzzleCamera != null)
            {
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
        }
        else
        {
            isAnyObjectFocused = false;
            currentlyFocusedObject = null;

            if (puzzleCamera == null) puzzleCamera = GameObject.Find("CinemachineCamera")?.GetComponent<CinemachineCamera>();
            if (sharedFocusCamera == null) sharedFocusCamera = GameObject.Find("FocusCamera")?.GetComponent<CinemachineCamera>();

            if (puzzleCamera != null) puzzleCamera.Priority = 20;
            if (sharedFocusCamera != null) sharedFocusCamera.Priority = 10;
        }

        // 💡 THE FIX: Automatically wait until the camera finishes moving!
        CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            yield return null; // Wait 1 frame for the transition to begin
            yield return new WaitWhile(() => brain.IsBlending);
        }
        else
        {
            yield return new WaitForSeconds(2.0f); // Fallback
        }

        isTransitioning = false; // 🔓 UNLOCK the camera
    }

    private void Update()
    {
        if (!isAnyObjectFocused || currentlyFocusedObject != this || isTransitioning) return;

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
                        StartCoroutine(TransitionRoutine(false));
                    }
                }
            }
            else
            {
                StartCoroutine(TransitionRoutine(false));
            }
        }
    }

    private void OnDestroy()
    {
        if (currentlyFocusedObject == this)
        {
            isTransitioning = false;
            isAnyObjectFocused = false;
        }
    }
}