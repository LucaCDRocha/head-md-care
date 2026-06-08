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
            Debug.LogError($"StartGame on {gameObject.name} cannot find your primary Puzzle Camera!");
            return;
        }

        isStarting = true;
        StartCoroutine(StartSequence());
    }

    private IEnumerator StartSequence()
    {
        // 1. OPEN THE DOOR
        Transform doorParent = transform.parent;
        if (doorParent == null) doorParent = transform;

        // Memorize exactly where the door started so we can close it later!
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

        // 3. SHIFT CAMERAS
        Debug.Log("Door open! Shifting camera perspective.");
        puzzleCamera.Priority = 30;
        if (menuCamera != null) menuCamera.Priority = 10;

        // 💡 4. THE AUTO-CLOSE FIX
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
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            enabled = false;
        }
    }
}