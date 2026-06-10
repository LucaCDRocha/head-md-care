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
    public AudioSource doorOpenAudio; 
    
    [Tooltip("This sound will start at volume 0 and fade up to 1 as the door opens, and continue playing!")]
    public AudioSource cafeAmbience;

    [Tooltip("Add any other sounds here (like the rain) that should instantly stop when the door cuts.")]
    public AudioSource[] ambientSounds; 

    [Header("Door Animation")]
    public float openDuration = 1.2f;
    public float openAngle = 90f;

    [Header("Sign Settings")] // 💡 NEW: Added the sign variables
    public GameObject openSign;
    public GameObject closedSign;

    [Header("Optional Settings")]
    public bool hideOnStart = true;

    private bool isStarting = false;

    private void Start()
    {
        Application.targetFrameRate = 30;
        
        // Guarantee the cafe ambience is totally silent before the player clicks the door
        if (cafeAmbience != null)
        {
            cafeAmbience.volume = 0f;
        }

        // 💡 NEW: Guarantee the shop is visually "Open" when the game starts or restarts
        if (openSign != null) openSign.SetActive(true);
        if (closedSign != null) closedSign.SetActive(false);
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

        // Start playing the cafe sound, but at 0 volume!
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

            // Swing the door
            doorParent.rotation = Quaternion.Lerp(startRotation, endRotation, smoothT);
            
            // Fade the volume up perfectly alongside the door animation!
            if (cafeAmbience != null)
            {
                cafeAmbience.volume = Mathf.Lerp(0f, 1f, smoothT);
            }

            yield return null;
        }

        doorParent.rotation = endRotation;
        
        // Ensure volume is exactly 1 when finished
        if (cafeAmbience != null) cafeAmbience.volume = 1f;

        // 3. DRAMATIC PAUSE
        yield return new WaitForSeconds(0.2f);

        // 4. SHIFT CAMERAS (And let the Cafe Ambience keep playing!)
        puzzleCamera.Priority = 30;
        if (menuCamera != null) menuCamera.Priority = 10;
        
        // 💡 NEW: Switch the signs exactly when the camera cuts down to the table!
        if (openSign != null) openSign.SetActive(false);
        if (closedSign != null) closedSign.SetActive(true);
        
        // Stop the rain and anything else in the array
        foreach (AudioSource audio in ambientSounds)
        {
            if (audio != null) audio.Stop();
        }

        yield return null;

        doorParent.rotation = startRotation;

        // 5. CLEANUP
        if (hideOnStart) gameObject.SetActive(false);
        else isStarting = false; 
    }
}