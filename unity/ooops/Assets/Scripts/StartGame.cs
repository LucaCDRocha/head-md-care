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
    [Tooltip("Drag the Coffee Mug here. It will stay hidden until the intro audio finishes.")]
    public GameObject coffeeMug; // 💡 NEW: The mug reference!

    [Header("Optional Settings")]
    public bool hideOnStart = true;

    private bool isStarting = false;

    private void Start()
    {
        Application.targetFrameRate = 30;
        
        if (cafeAmbience != null)
        {
            cafeAmbience.volume = 0f;
        }

        if (openSign != null) openSign.SetActive(true);
        if (closedSign != null) closedSign.SetActive(false);

        // 💡 NEW: Instantly hide the coffee mug when the game loads!
        if (coffeeMug != null) coffeeMug.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isStarting) return;

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
        if (doorOpenAudio != null) doorOpenAudio.Play();

        if (cafeAmbience != null)
        {
            cafeAmbience.volume = 0f;
            cafeAmbience.Play();
        }

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
            
            if (cafeAmbience != null)
            {
                cafeAmbience.volume = Mathf.Lerp(0f, 1f, smoothT);
            }

            yield return null;
        }

        doorParent.rotation = endRotation;
        
        if (cafeAmbience != null) cafeAmbience.volume = 1f;

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

        // 💡 NEW: Pause the sequence and wait for the intro sound to finish speaking!
        if (introCutSound != null)
        {
            yield return new WaitWhile(() => introCutSound.isPlaying);
        }

        // 💡 NEW: Now that they are done talking, reveal the coffee mug!
        if (coffeeMug != null) coffeeMug.SetActive(true);

        yield return null;

        doorParent.rotation = startRotation;

        // 5. CLEANUP
        if (hideOnStart) gameObject.SetActive(false);
        else isStarting = false; 
    }
}