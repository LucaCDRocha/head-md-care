using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using TMPro;

public class ObjectFocus : MonoBehaviour, IPointerClickHandler
{
    [Header("Cinemachine Cameras")]
    public CinemachineCamera puzzleCamera;
    public CinemachineCamera sharedFocusCamera;

    [Header("Framing Settings")]
    public Vector3 cameraLocalOffset = new Vector3(0f, 0.5f, -1.5f);

    [Header("Inspection Audio")]
    public AudioSource inspectionAudio;

    [Header("ID Card System")]
    [TextArea(3, 5)]
    public string idCardDescription;
    public GameObject idCardPanel;
    public TextMeshProUGUI idCardTextUI;

    [Header("Ambience Ducking Settings")]
    [Range(0f, 1f)]
    public float duckedVolumeMultiplier = 0.15f;
    public float audioFadeDuration = 0.5f;

    private static bool isAnyObjectFocused = false;
    private static ObjectFocus currentlyFocusedObject = null;
    private static bool isTransitioning = false;

    private PuzzleLogic puzzleLogic;
    private Coroutine audioMonitorCoroutine;
    private Coroutine ambienceFadeCoroutine;
    private float preDuckedVolume = 1f;
    private bool didDuckAmbience = false;

    private void Start()
    {
        puzzleLogic = FindAnyObjectByType<PuzzleLogic>();
        if (idCardPanel != null) idCardPanel.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (puzzleLogic != null && puzzleLogic.isShattering) return;
        if (puzzleLogic != null && !puzzleLogic.hasExploded && !puzzleLogic.isRestored) return;
        if (isTransitioning) return;

        InteractablePulse pulseScript = GetComponent<InteractablePulse>();
        if (pulseScript != null)
        {
            pulseScript.StopPulsingPermanently();
        }

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
            if (inspectionAudio != null)
            {
                // We keep the ambience ducking here so the room gets quiet DURING the camera flight
                if (puzzleLogic != null && puzzleLogic.cafeAmbience != null)
                {
                    didDuckAmbience = true;
                    preDuckedVolume = puzzleLogic.cafeAmbience.volume;
                    float targetDuckedVolume = preDuckedVolume * duckedVolumeMultiplier;

                    if (ambienceFadeCoroutine != null) StopCoroutine(ambienceFadeCoroutine);
                    ambienceFadeCoroutine = StartCoroutine(FadeAmbienceRoutine(puzzleLogic.cafeAmbience, targetDuckedVolume, audioFadeDuration));
                }
            }
            else
            {
                didDuckAmbience = false;
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

            if (idCardPanel != null) idCardPanel.SetActive(false);

            if (didDuckAmbience && puzzleLogic != null && puzzleLogic.cafeAmbience != null)
            {
                if (ambienceFadeCoroutine != null) StopCoroutine(ambienceFadeCoroutine);
                ambienceFadeCoroutine = StartCoroutine(FadeAmbienceRoutine(puzzleLogic.cafeAmbience, preDuckedVolume, audioFadeDuration));
            }

            didDuckAmbience = false;

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

        // --- CAMERA HAS ARRIVED ---

        if (isFocusing)
        {
            // Show the UI Card
            if (!string.IsNullOrWhiteSpace(idCardDescription) && idCardPanel != null && idCardTextUI != null)
            {
                idCardTextUI.text = idCardDescription;
                idCardPanel.SetActive(true);
            }

            // 💡 THE FIX: Play the audio NOW, after the camera has completely finished moving!
            if (inspectionAudio != null)
            {
                inspectionAudio.Stop();
                inspectionAudio.Play();

                // Start monitoring the audio so we know when to auto-exit
                if (audioMonitorCoroutine != null) StopCoroutine(audioMonitorCoroutine);
                audioMonitorCoroutine = StartCoroutine(MonitorAudioRoutine());
            }
        }

        isTransitioning = false;

        // --- CAMERA HAS RETURNED ---
        
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
            StartCoroutine(TransitionRoutine(false));
        }
    }

    private IEnumerator FadeAmbienceRoutine(AudioSource source, float targetVolume, float duration)
    {
        if (source == null) yield break;

        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
            yield return null;
        }

        source.volume = targetVolume;
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