using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class EndGameSequence : MonoBehaviour, IPointerClickHandler
{
    [Header("Logic Connection")]
    public PuzzleLogic puzzleLogic;

    [Header("Cinematics")]
    [Tooltip("The camera that looks at the door or window for the final shot.")]
    public CinemachineCamera outroCamera;

    [Tooltip("How long to wait staring at the closed door before fading to black.")]
    public float pauseBeforeFade = 2.0f;

    public float fadeDuration = 2.0f;
    public float pauseBeforeReset = 2.0f;
    public Color fadeColor = Color.black;

    [Header("Door Animation")]
    [Tooltip("Drag the physical Door object here.")]
    public Transform doorTransform; // 💡 NEW: The door we want to control

    [Tooltip("The angle it should be open at when the camera cuts to it.")]
    public float doorOpenAngle = 90f;

    [Tooltip("How long it takes to swing shut.")]
    public float doorCloseDuration = 1.2f;

    [Header("Audio")]
    public AudioSource coinSound;
    public AudioSource rainSound;
    public AudioSource doorCloseAudio; // 💡 NEW: The door shutting sound

    private bool isEnding = false;
    private Quaternion initialDoorRotation;

    private void Start()
    {
        if (puzzleLogic == null) puzzleLogic = FindAnyObjectByType<PuzzleLogic>();

        // Memorize the exact rotation of the door while it is closed at the start!
        if (doorTransform != null)
        {
            initialDoorRotation = doorTransform.rotation;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (puzzleLogic == null || !puzzleLogic.isRestored) return;

        if (isEnding) return;
        isEnding = true;

        StartCoroutine(FinaleRoutine());
    }

    private IEnumerator FinaleRoutine()
    {
        // 1. Audio begins
        if (coinSound != null) coinSound.Play();

        if (rainSound != null)
        {
            rainSound.volume = 1f;
            if (!rainSound.isPlaying) rainSound.Play();
        }

        // 2. INVISIBLY SNAP THE DOOR OPEN
        // (Because the camera is still looking at the table, the player won't see this happen!)
        if (doorTransform != null)
        {
            doorTransform.rotation = initialDoorRotation * Quaternion.Euler(0, doorOpenAngle, 0);
        }

        // 3. INSTANT CUT to the Outro Camera
        if (outroCamera != null)
        {
            CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();

            if (brain != null)
            {
                var originalBlend = brain.DefaultBlend;
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
                outroCamera.Priority = 50;
                yield return null; // Wait exactly 1 frame for the screen to update
                brain.DefaultBlend = originalBlend;
            }
            else
            {
                outroCamera.Priority = 50;
            }
        }

        // 4. Give the player half a second to register the open door and the rain
        yield return new WaitForSeconds(0.4f);

        // 5. ANIMATE THE DOOR SWINGING SHUT
        if (doorTransform != null)
        {
            // (We removed the Play() command from here!)

            float elapsedDoor = 0f;
            Quaternion openRotation = doorTransform.rotation;

            while (elapsedDoor < doorCloseDuration)
            {
                elapsedDoor += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedDoor / doorCloseDuration);
                float smoothT = t * t * (3f - 2f * t); // Smooth easing

                doorTransform.rotation = Quaternion.Lerp(openRotation, initialDoorRotation, smoothT);
                yield return null;
            }

            // Guarantee it is perfectly locked shut
            doorTransform.rotation = initialDoorRotation;

            // 🔊 THE FIX: Play the sound the exact millisecond the door hits the frame!
            if (doorCloseAudio != null) doorCloseAudio.Play();
        }

        // 6. Hold on the closed door
        yield return new WaitForSeconds(pauseBeforeFade);

        // 7. Fade Canvas
        GameObject fadeObj = new GameObject("EndGameFadeCanvas");
        Canvas canvas = fadeObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        Image fadeImage = fadeObj.AddComponent<Image>();
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);

        // 8. Fade to black
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeDuration);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }

        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);

        // 9. Final Pause
        yield return new WaitForSeconds(pauseBeforeReset);

        // 10. Restart Game
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}