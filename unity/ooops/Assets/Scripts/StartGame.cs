using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine;

public class StartGame : MonoBehaviour, IPointerClickHandler
{
    [Header("Cinemachine Cameras")]
    public CinemachineCamera menuCamera;
    public CinemachineCamera puzzleCamera;

    [Header("Starting Room Audio")]
    public AudioSource introCutSound;
    public AudioSource doorOpenAudio; 
    
    [Tooltip("This sound will start at volume 0 and fade up to 1 as the door opens, and continue playing!")]
    public AudioSource cafeAmbience;

    [Tooltip("Add any other sounds here (like the rain) that should instantly stop when the door cuts.")]
    public AudioSource[] ambientSounds; 

    [Header("Door Animation")]
    public float openDuration = 1.2f;
    public float openAngle = 90f;

    [Header("Sign Settings")]
    public GameObject openSign;
    public GameObject closedSign;

    [Header("Puzzle Elements")]
    public GameObject coffeeMug; 

    [Header("Optional Settings")]
    public bool hideOnStart = true;

    private bool isStarting = false;
    private Collider doorCollider; // 💡 NEW: We need to reference the collider!
    private float[] maxAmbienceVolumes;

    private void Start()
    {
        Application.targetFrameRate = 30;
        
        // 💡 NEW: Automatically find the collider on this object and make sure it is ON!
        doorCollider = GetComponent<Collider>();
        if (doorCollider != null)
        {
            doorCollider.enabled = true;
        }

        CacheAmbienceMaxVolumes();
        SetAudioVolume(cafeAmbience, 0f);

        if (openSign != null) openSign.SetActive(true);
        if (closedSign != null) closedSign.SetActive(false);

        if (coffeeMug != null) coffeeMug.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isStarting) return;

        // 💡 THE FIX: Instantly disable the collider the exact millisecond the player clicks!
        if (doorCollider != null)
        {
            doorCollider.enabled = false;
        }

        if (puzzleCamera == null)
        {
            puzzleCamera = GameObject.Find("CinemachineCamera")?.GetComponent<CinemachineCamera>();
        }

        if (puzzleCamera == null) return;

        isStarting = true;
        StartCoroutine(StartGameSequence());
    }

    private IEnumerator StartGameSequence()
    {
        // 1. PLAY INITIAL SOUNDS
        PlayAllAudioSources(doorOpenAudio);

        SetAudioVolume(cafeAmbience, 0f);
        PlayAllAudioSources(cafeAmbience);

        // 2. OPEN THE DOOR AND FADE VOLUME
        Transform doorParent = transform.parent != null ? transform.parent : transform;
        Quaternion startRotation = doorParent.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(0, openAngle, 0);

        float elapsed = 0f;

        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / openDuration);
            float smoothT = t * t * (3f - 2f * t);

            doorParent.rotation = Quaternion.Lerp(startRotation, endRotation, smoothT);
            
            SetAudioVolume(cafeAmbience, Mathf.Lerp(0f, 1f, smoothT));

            yield return null;
        }

        doorParent.rotation = endRotation;
        
        SetAudioVolume(cafeAmbience, 1f);

        // 3. DRAMATIC PAUSE
        yield return new WaitForSeconds(0.2f);

        // 4. SHIFT CAMERAS
        puzzleCamera.Priority = 30;
        if (menuCamera != null) menuCamera.Priority = 10;

        if (introCutSound != null) introCutSound.Play();
        
        if (openSign != null) openSign.SetActive(false);
        if (closedSign != null) closedSign.SetActive(true);
        
        foreach (AudioSource audio in ambientSounds)
        {
            if (audio != null) audio.Stop();
        }

        if (introCutSound != null)
        {
            yield return new WaitWhile(() => introCutSound.isPlaying);
        }

        if (coffeeMug != null) coffeeMug.SetActive(true);

        yield return null;

        doorParent.rotation = startRotation;

        // 5. CLEANUP
        if (hideOnStart) gameObject.SetActive(false);
        else isStarting = false; 
    }

    private void CacheAmbienceMaxVolumes()
    {
        if (cafeAmbience == null) return;
        AudioSource[] sources = cafeAmbience.GetComponentsInChildren<AudioSource>(true);
        if (sources == null || sources.Length == 0) sources = new AudioSource[] { cafeAmbience };

        maxAmbienceVolumes = new float[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            maxAmbienceVolumes[i] = sources[i] != null ? sources[i].volume : 1f;
        }
    }

    private void SetAudioVolume(AudioSource source, float volumeFactor)
    {
        if (source == null) return;
        AudioSource[] sources = source.GetComponentsInChildren<AudioSource>(true);
        if (sources == null || sources.Length == 0) sources = new AudioSource[] { source };

        if (source == cafeAmbience && maxAmbienceVolumes != null && maxAmbienceVolumes.Length == sources.Length)
        {
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null) sources[i].volume = maxAmbienceVolumes[i] * volumeFactor;
            }
        }
        else
        {
            foreach (AudioSource src in sources)
            {
                if (src != null) src.volume = volumeFactor;
            }
        }
    }

    private void PlayAllAudioSources(AudioSource source)
    {
        if (source == null) return;
        AudioSource[] sources = source.GetComponentsInChildren<AudioSource>(true);
        if (sources == null || sources.Length == 0)
        {
            if (source.gameObject.activeInHierarchy) source.Play();
            return;
        }
        foreach (AudioSource src in sources)
        {
            if (src != null && src.gameObject.activeInHierarchy) src.Play();
        }
    }
}