using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine;

public class StartGame : MonoBehaviour, IPointerClickHandler
{
    [Header("Cinemachine Cameras")]
    [Tooltip("The camera looking at the main menu or starting shot.")]
    public CinemachineCamera menuCamera;

    [Tooltip("The game camera looking at the puzzle vase that we want to switch to instantly.")]
    public CinemachineCamera puzzleCamera;

    [Header("Starting Room Audio")]
    [Tooltip("Add as many Audio Sources here as you want! They will all instantly stop when the door opens.")]
    public AudioSource[] ambientSounds; // 💡 NEW: The Array of sounds to silence!

    [Header("Door Animation")]
    [Tooltip("How long it takes the door to swing open (in seconds).")]
    public float openDuration = 1.2f;

    [Tooltip("How many degrees the door should swing. Change to -90 if it swings the wrong way!")]
    public float openAngle = 90f;

    [Header("Optional Settings")]
    [Tooltip("If true, the script will automatically hide the clickable hitbox after it is clicked.")]
    public bool hideOnStart = true;

    private bool isStarting = false;

    private void Start()
    {
        Application.targetFrameRate = 30;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isStarting) return;

        if (puzzleCamera == null)
        {
            puzzleCamera = GameObject.Find("CinemachineCamera")?.GetComponent<CinemachineCamera>();
        }

        if (puzzleCamera == null)
        {
            Debug.LogError("No CinemachineCamera found for the puzzle view!");
            return;
        }

        isStarting = true;
        StartCoroutine(StartGameSequence());
    }

    private IEnumerator StartGameSequence()
    {
        // 1. OPEN THE DOOR
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
            yield return null;
        }

        doorParent.rotation = endRotation;

        // 2. DRAMATIC PAUSE
        yield return new WaitForSeconds(0.2f);

        // 3. SHIFT CAMERAS & SILENCE AUDIO
        Debug.Log("Door open! Shifting camera perspective and stopping ambience.");
        puzzleCamera.Priority = 30;
        if (menuCamera != null) menuCamera.Priority = 10;

        // 🔊 THE FIX: Loop through your list and tell every single sound to stop!
        foreach (AudioSource audio in ambientSounds)
        {
            if (audio != null) 
            {
                audio.Stop();
            }
        }

        // Wait exactly 1 frame to guarantee Cinemachine has switched the screen...
        yield return null;

        // ...and instantly snap the door back to its original closed position invisibly!
        doorParent.rotation = startRotation;

        // 5. CLEANUP
        if (hideOnStart)
        {
            gameObject.SetActive(false);
        }
        else
        {
            isStarting = false; 
        }
    }
}