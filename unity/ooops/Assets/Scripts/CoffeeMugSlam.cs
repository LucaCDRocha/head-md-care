using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class CoffeeMugSlam : MonoBehaviour, IPointerClickHandler
{
    [Header("Puzzle Connection")]
    public PuzzleLogic puzzleLogic;

    [Header("Audio Sources (Drag your empty Audio child objects here!)")]
    [Tooltip("The sound that plays while the mug is magically floating up.")]
    public AudioSource magicHoverAudio;
    
    [Tooltip("The heavy thud sound when it hits the table.")]
    public AudioSource slamAudio;

    [Header("Animation Timings")]
    public float floatUpTime = 1.0f;
    public float pauseTime = 0.5f;
    public float slamDownTime = 0.15f; 

    [Header("Camera Hover Settings")]
    public float distanceFromCamera = 1.5f;
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
        Quaternion hoverRotation = mainCamera.transform.rotation * Quaternion.Euler(hoverRotationOffset);

        // 🔊 TRIGGER HOVER SOUND
        if (magicHoverAudio != null) magicHoverAudio.Play();

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

        // 🔊 TRIGGER SLAM SOUND (The exact millisecond it hits the table!)
        if (magicHoverAudio != null) magicHoverAudio.Stop(); // Stop the magic hum
        if (slamAudio != null) slamAudio.Play(); // Play the thud!

        // --- PHASE 4: TRIGGER THE SHATTER! ---
        if (puzzleLogic != null)
        {
            puzzleLogic.StartPuzzleChaos();
        }
    }
}