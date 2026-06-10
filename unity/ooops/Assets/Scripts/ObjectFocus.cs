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

    [Header("Inspection Audio")]
    public AudioSource inspectionAudio;

    private static bool isAnyObjectFocused = false;
    private static ObjectFocus currentlyFocusedObject = null;
    private static bool isTransitioning = false;
    
    private PuzzleLogic puzzleLogic;
    private Coroutine audioMonitorCoroutine;

    private void Start()
    {
        puzzleLogic = FindAnyObjectByType<PuzzleLogic>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 1. Block clicks if the shatter sequence is currently playing
        if (puzzleLogic != null && puzzleLogic.isShattering) return;

        // 2. Allow clicks ONLY if the puzzle is exploded OR fully restored
        if (puzzleLogic != null && !puzzleLogic.hasExploded && !puzzleLogic.isRestored) return;
        
        // 3. Block clicks if the camera is currently flying
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
        isTransitioning = true; 

        if (isFocusing)
        {
            // Start the audio immediately before the camera moves (swells up)
            if (inspectionAudio != null)
            {
                inspectionAudio.Stop(); 
                inspectionAudio.Play();
                
                if (audioMonitorCoroutine != null) StopCoroutine(audioMonitorCoroutine);
                audioMonitorCoroutine = StartCoroutine(MonitorAudioRoutine());
            }

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

        CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            yield return null; 
            yield return new WaitWhile(() => brain.IsBlending);
        }
        else
        {
            yield return new WaitForSeconds(2.0f); 
        }

        isTransitioning = false; 

        // Stop the audio ONLY after the camera has fully returned (fades out)
        if (!isFocusing && inspectionAudio != null)
        {
            inspectionAudio.Stop();
            if (audioMonitorCoroutine != null) StopCoroutine(audioMonitorCoroutine);
        }
    }

    private IEnumerator MonitorAudioRoutine()
    {
        yield return new WaitForSeconds(0.1f);

        while (inspectionAudio != null && inspectionAudio.isPlaying)
        {
            yield return null;
        }

        if (currentlyFocusedObject == this && !isTransitioning)
        {
            Debug.Log("Audio finished! Auto-exiting focus mode.");
            StartCoroutine(TransitionRoutine(false));
        }
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