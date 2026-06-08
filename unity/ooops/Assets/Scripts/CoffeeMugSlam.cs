using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class CoffeeMugSlam : MonoBehaviour, IPointerClickHandler
{
    [Header("Puzzle Connection")]
    [Tooltip("Drag your PuzzleLogic object here so the mug knows what to explode!")]
    public PuzzleLogic puzzleLogic;

    [Header("Animation Timings")]
    public float floatUpTime = 1.0f;
    public float pauseTime = 0.5f;
    public float slamDownTime = 0.15f; 

    [Header("Camera Hover Settings")]
    [Tooltip("How far in front of the camera the mug should float.")]
    public float distanceFromCamera = 1.5f;
    
    // 💡 NEW: Let's you fix weird 3D model import angles directly in the Inspector!
    [Tooltip("Tweak these X, Y, Z angles to spin and tilt the mug perfectly towards the camera.")]
    public Vector3 hoverRotationOffset = new Vector3(15f, 180f, 0f);

    private Camera mainCamera;
    private bool hasTriggered = false;

    private void Start()
    {
        mainCamera = Camera.main;
        if (puzzleLogic == null) puzzleLogic = FindAnyObjectByType<PuzzleLogic>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (hasTriggered) return;
        hasTriggered = true;

        StartCoroutine(SlamSequence());
    }

    private IEnumerator SlamSequence()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        Vector3 tablePosition = transform.position;
        Quaternion tableRotation = transform.rotation;

        Vector3 hoverPosition = mainCamera.transform.position + (mainCamera.transform.forward * distanceFromCamera);
        
        // 💡 NEW: Uses your custom Inspector angles so you can face the mug perfectly!
        Quaternion hoverRotation = mainCamera.transform.rotation * Quaternion.Euler(hoverRotationOffset);

        // --- PHASE 1: FLOAT UP ---
        float elapsed = 0f;
        while (elapsed < floatUpTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / floatUpTime); 
            
            transform.position = Vector3.Lerp(tablePosition, hoverPosition, t);
            transform.rotation = Quaternion.Slerp(tableRotation, hoverRotation, t);
            yield return null;
        }

        // --- PHASE 2: DRAMATIC PAUSE ---
        yield return new WaitForSeconds(pauseTime);

        // --- PHASE 3: THE VIOLENT SLAM ---
        elapsed = 0f;
        while (elapsed < slamDownTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slamDownTime;
            float easeIn = t * t * t; 

            transform.position = Vector3.Lerp(hoverPosition, tablePosition, easeIn);
            transform.rotation = Quaternion.Slerp(hoverRotation, tableRotation, easeIn);
            yield return null;
        }

        transform.position = tablePosition;
        transform.rotation = tableRotation;

        // --- PHASE 4: TRIGGER THE SHATTER! ---
        if (puzzleLogic != null)
        {
            puzzleLogic.StartPuzzleChaos();
        }

        // 💡 FIXED: The SetActive(false) line is gone! The mug will now stay on the table forever.
    }
}