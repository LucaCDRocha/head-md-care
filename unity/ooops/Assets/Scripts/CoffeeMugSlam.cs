using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class CoffeeMugSlam : MonoBehaviour, IPointerClickHandler
{
    [Header("Puzzle Connection")]
    public PuzzleLogic puzzleLogic;

    [Header("Audio Sources")]
    public AudioSource magicHoverAudio;
    public AudioSource slamAudio;

    [Header("Animation Timings")]
    public float floatUpTime = 1.0f;
    public float pauseTime = 0.5f;
    public float slamDownTime = 0.15f;

    [Header("Camera Hover Settings")]
    public float distanceFromCamera = 1.5f;
    public Vector3 rotationOffset = new Vector3(15f, 180f, 0f);

    [Tooltip("Fine-tune the mug's position relative to the camera (X=Side, Y=Up/Down, Z=Forward/Backward).")]
    public Vector3 translationOffset = new Vector3(0f, 0f, 0f); // 💡 NEW: The fine-tuning offset!

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

        // Calculate the base hover position
        Vector3 baseHoverPos = mainCamera.transform.position + (mainCamera.transform.forward * distanceFromCamera);

        // 💡 THE FIX: Apply the offset relative to the camera's rotation
        Vector3 finalHoverPosition = baseHoverPos + (mainCamera.transform.right * translationOffset.x) +
                                     (mainCamera.transform.up * translationOffset.y) +
                                     (mainCamera.transform.forward * translationOffset.z);

        Quaternion hoverRotation = mainCamera.transform.rotation * Quaternion.Euler(rotationOffset);

        if (magicHoverAudio != null) magicHoverAudio.Play();

        float elapsed = 0f;
        while (elapsed < floatUpTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / floatUpTime);

            transform.position = Vector3.Lerp(tablePosition, finalHoverPosition, t);
            transform.rotation = Quaternion.Slerp(tableRotation, hoverRotation, t);
            yield return null;
        }

        yield return new WaitForSeconds(pauseTime);

        elapsed = 0f;
        while (elapsed < slamDownTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / slamDownTime;
            float easeIn = t * t * t;

            transform.position = Vector3.Lerp(finalHoverPosition, tablePosition, easeIn);
            transform.rotation = Quaternion.Slerp(hoverRotation, tableRotation, easeIn);
            yield return null;
        }

        transform.position = tablePosition;
        transform.rotation = tableRotation;

        if (magicHoverAudio != null) magicHoverAudio.Stop();
        if (slamAudio != null) slamAudio.Play();

        if (puzzleLogic != null) puzzleLogic.StartPuzzleChaos();
    }
}