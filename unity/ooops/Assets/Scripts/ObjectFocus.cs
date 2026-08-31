using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using TMPro; // Crucial for 3D Text!

public class ObjectFocus : MonoBehaviour, IPointerClickHandler
{
    [Header("Cinemachine Cameras")]
    public CinemachineCamera puzzleCamera;
    public CinemachineCamera sharedFocusCamera;

    [Header("Framing Settings")]
    public Vector3 cameraLocalOffset = new Vector3(0f, 0.5f, -1.5f);

    [Header("Inspection & Camera Audio")]
    public AudioSource inspectionAudio;

    [Tooltip("Optional: AudioSource played immediately when zooming into this object.")]
    public AudioSource zoomInAudio;

    [Tooltip("Optional: AudioSource played immediately when zooming out back to puzzle camera.")]
    public AudioSource zoomOutAudio;

    [Header("ID Card Lore (Script)")]
    [Tooltip("The main header, like a title or person's name.")]
    public string cardName;

    [Tooltip("The sender or origin point.")]
    public string cardFromWhom;

    [TextArea(5, 10)]
    [Tooltip("The main body of text for the ID Card.")]
    public string cardMainDescription;

    [Header("ID Card Lore (French Overrides)")]
    [Tooltip("The French title/name.")]
    public string cardNameFrench;

    [Tooltip("The French sender or origin point.")]
    public string cardFromWhomFrench;

    [TextArea(5, 10)]
    [Tooltip("The French main body of text for the ID Card.")]
    public string cardMainDescriptionFrench;

    [Header("ID Card Visuals (TextMeshPro)")]
    [Tooltip("Drag the PARENT container of your 3D text objects here (the background/holder).")]
    public GameObject idCardPanel;

    [Tooltip("Drag the TMPro object for the Name field here.")]
    public TextMeshPro nameTextUI;

    [Tooltip("Drag the TMPro object for the From Whom field here.")]
    public TextMeshPro fromWhomTextUI;

    [Tooltip("Drag the TMPro object for the Description field here.")]
    public TextMeshPro descriptionTextUI;

    [Header("3D ID Card Placement")]
    // 💡 THE FIX: Setting new defaults based on image_2.png
    public float cardDistance = 0.5f;
    public Vector2 cardOffset = new Vector2(0.22f, -0.18f);
    public Vector3 cardRotationOffset = new Vector3(-90f, -90f, 0f);

    [Tooltip("1 is normal size. 0.5 is half size. 2 is double size.")]
    public float cardScale = 1.0f;

    [Header("Ambience Ducking Settings")]
    [Range(0f, 1f)]
    public float duckedVolumeMultiplier = 0.15f;
    public float audioFadeDuration = 0.5f;

    private static bool isAnyObjectFocused = false;
    private static ObjectFocus currentlyFocusedObject = null;
    public static bool isTransitioning = false;
    public static float ignoreFocusClicksUntil = 0f;

    public static bool IsAnyObjectFocused => isAnyObjectFocused;
    public static ObjectFocus CurrentlyFocusedObject => currentlyFocusedObject;

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

    public void Unfocus()
    {
        if (!isTransitioning && isAnyObjectFocused && currentlyFocusedObject == this)
        {
            ignoreFocusClicksUntil = Time.time + 0.5f;
            StartCoroutine(TransitionRoutine(false));
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (puzzleLogic != null && puzzleLogic.isShattering) return;
        if (puzzleLogic != null && !puzzleLogic.hasExploded && !puzzleLogic.isRestored) return;
        if (isTransitioning) return;
        if (Time.time < ignoreFocusClicksUntil) return;

        // If another object is currently in focus, trigger unfocus on that focused object and DO NOT focus this object
        if (isAnyObjectFocused && currentlyFocusedObject != this)
        {
            if (currentlyFocusedObject != null)
            {
                currentlyFocusedObject.Unfocus();
            }
            else
            {
                ignoreFocusClicksUntil = Time.time + 0.5f;
            }
            return;
        }

        InteractablePulse pulseScript = GetComponent<InteractablePulse>();
        if (pulseScript != null)
        {
            pulseScript.StopPulsingPermanently();
        }

        if (isAnyObjectFocused && currentlyFocusedObject == this)
        {
            Unfocus();
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
            isAnyObjectFocused = true;
            currentlyFocusedObject = this;
            PlaySoundEffect(zoomInAudio);

            if (inspectionAudio != null)
            {
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

            if (puzzleCamera == null)
            {
                puzzleCamera = GameObject.Find("PuzzleCamera")?.GetComponent<CinemachineCamera>() 
                            ?? GameObject.Find("CinemachineCamera")?.GetComponent<CinemachineCamera>();
            }
            if (sharedFocusCamera == null)
            {
                sharedFocusCamera = GameObject.Find("FocusCamera")?.GetComponent<CinemachineCamera>();
            }

            if (sharedFocusCamera != null)
            {
                Vector3 rotatedOffset = transform.rotation * cameraLocalOffset;
                Vector3 targetPos = transform.position + rotatedOffset;
                Quaternion targetRot = Quaternion.LookRotation(transform.position - targetPos);

                sharedFocusCamera.transform.position = targetPos;
                sharedFocusCamera.transform.rotation = targetRot;

                sharedFocusCamera.Priority = 20;
            }
            if (puzzleCamera != null)
            {
                puzzleCamera.Priority = 10;
            }
        }
        else
        {
            PlaySoundEffect(zoomOutAudio);
            if (idCardPanel != null) idCardPanel.SetActive(false);

            if (didDuckAmbience && puzzleLogic != null && puzzleLogic.cafeAmbience != null)
            {
                if (ambienceFadeCoroutine != null) StopCoroutine(ambienceFadeCoroutine);
                ambienceFadeCoroutine = StartCoroutine(FadeAmbienceRoutine(puzzleLogic.cafeAmbience, preDuckedVolume, audioFadeDuration));
            }

            didDuckAmbience = false;

            if (puzzleCamera == null)
            {
                puzzleCamera = GameObject.Find("PuzzleCamera")?.GetComponent<CinemachineCamera>() 
                            ?? GameObject.Find("CinemachineCamera")?.GetComponent<CinemachineCamera>();
            }
            if (sharedFocusCamera == null)
            {
                sharedFocusCamera = GameObject.Find("FocusCamera")?.GetComponent<CinemachineCamera>();
            }

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
            // 💡 THE FIX: We now check if the *NAME* is filled out. If so, populate the fields individually.
            if (!string.IsNullOrWhiteSpace(cardName) && idCardPanel != null)
            {
                bool isFrench = SubtitleManager.CurrentLanguage == Language.French;

                string displayName = (isFrench && !string.IsNullOrWhiteSpace(cardNameFrench)) ? cardNameFrench : cardName;
                string displayFrom = (isFrench && !string.IsNullOrWhiteSpace(cardFromWhomFrench)) ? cardFromWhomFrench : cardFromWhom;
                string displayDesc = (isFrench && !string.IsNullOrWhiteSpace(cardMainDescriptionFrench)) ? cardMainDescriptionFrench : cardMainDescription;
                string fromPrefix = isFrench ? "DE : " : "FROM: ";

                // Populate the new fields with individual null-checks for safety
                if (nameTextUI != null) nameTextUI.text = displayName;

                if (fromWhomTextUI != null) fromWhomTextUI.text = fromPrefix + displayFrom; // Prefix for visual clarity

                if (descriptionTextUI != null) descriptionTextUI.text = displayDesc;

                UpdateIDCardPlacement();
                idCardPanel.SetActive(true);
            }

            if (inspectionAudio != null)
            {
                inspectionAudio.Stop();
                inspectionAudio.Play();

                if (audioMonitorCoroutine != null) StopCoroutine(audioMonitorCoroutine);
                audioMonitorCoroutine = StartCoroutine(MonitorAudioRoutine());
            }
        }

        isTransitioning = false;

        if (!isFocusing)
        {
            isAnyObjectFocused = false;
            currentlyFocusedObject = null;

            if (inspectionAudio != null)
            {
                inspectionAudio.Stop();
                if (audioMonitorCoroutine != null) StopCoroutine(audioMonitorCoroutine);
            }
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

    private void UpdateIDCardPlacement()
    {
        if (sharedFocusCamera != null && idCardPanel != null)
        {
            Transform camTransform = sharedFocusCamera.transform;

            Vector3 targetPosition = camTransform.position
                                   + (camTransform.forward * cardDistance)
                                   + (camTransform.right * cardOffset.x)
                                   + (camTransform.up * cardOffset.y);

            idCardPanel.transform.position = targetPosition;

            idCardPanel.transform.rotation = camTransform.rotation * Quaternion.Euler(cardRotationOffset);

            idCardPanel.transform.localScale = Vector3.one * cardScale;
        }
    }

    private void Update()
    {
        if (isAnyObjectFocused && currentlyFocusedObject == this && !isTransitioning)
        {
            if (idCardPanel != null && idCardPanel.activeSelf)
            {
                UpdateIDCardPlacement();
            }
        }

        if (!isAnyObjectFocused || currentlyFocusedObject != this || isTransitioning) return;

        if (Pointer.current != null && Pointer.current.press.wasReleasedThisFrame)
        {
            Vector2 screenPosition = Pointer.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(screenPosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                bool isSelfOrChild = hit.transform == this.transform || hit.transform.IsChildOf(this.transform);
                bool isIDCard = idCardPanel != null && (hit.transform == idCardPanel.transform || hit.transform.IsChildOf(idCardPanel.transform));

                // If hit object has ObjectFocus or MandatoryClick, its OnPointerClick handles unfocusing.
                // Otherwise, handle unfocusing background environment clicks on pointer release.
                if (!isSelfOrChild && !isIDCard && hit.transform.GetComponent<ObjectFocus>() == null && hit.transform.GetComponent<MandatoryClick>() == null)
                {
                    Unfocus();
                }
            }
            else
            {
                Unfocus();
            }
        }
    }

    private void PlaySoundEffect(AudioSource customSource)
    {
        if (customSource == null) return;

        AudioSource[] sources = customSource.GetComponentsInChildren<AudioSource>(true);
        if (sources == null || sources.Length == 0)
        {
            if (customSource.gameObject.activeInHierarchy)
            {
                if (customSource.clip != null) customSource.PlayOneShot(customSource.clip);
                else customSource.Play();
            }
            return;
        }

        foreach (AudioSource src in sources)
        {
            if (src == null || !src.gameObject.activeInHierarchy) continue;
            if (src.clip != null) src.PlayOneShot(src.clip);
            else src.Play();
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