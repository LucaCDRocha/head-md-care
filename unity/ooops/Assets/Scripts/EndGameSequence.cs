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
    public Transform doorTransform; 
    public float doorOpenAngle = 90f;
    public float doorCloseDuration = 1.2f;

    [Header("Audio")]
    public AudioSource coinSound;
    public AudioSource rainSound; 
    public AudioSource doorCloseAudio; 

    private bool isEnding = false;
    private Quaternion initialDoorRotation;

    private void Start()
    {
        if (puzzleLogic == null) puzzleLogic = FindAnyObjectByType<PuzzleLogic>();

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
        // 💡 THE FIX 1: Instantly kill the inside cafe sounds the millisecond the receipt is clicked!
        if (puzzleLogic != null && puzzleLogic.cafeAmbience != null)
        {
            puzzleLogic.cafeAmbience.Stop();
        }

        // 1. Audio begins
        if (coinSound != null) coinSound.Play();

        if (rainSound != null)
        {
            rainSound.volume = 1f;
            if (!rainSound.isPlaying) rainSound.Play();
        }

        // 2. INVISIBLY SNAP THE DOOR OPEN
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
                yield return null; 
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
            float elapsedDoor = 0f;
            Quaternion openRotation = doorTransform.rotation;

            while (elapsedDoor < doorCloseDuration)
            {
                elapsedDoor += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedDoor / doorCloseDuration);
                float smoothT = t * t * (3f - 2f * t); 
                
                doorTransform.rotation = Quaternion.Lerp(openRotation, initialDoorRotation, smoothT);
                yield return null;
            }
            
            doorTransform.rotation = initialDoorRotation;

            if (doorCloseAudio != null) doorCloseAudio.Play();
        }

        // 6. Hold on the closed door
        yield return new WaitForSeconds(pauseBeforeFade);

        // 7. Fade Canvas setup
        GameObject fadeObj = new GameObject("EndGameFadeCanvas");
        Canvas canvas = fadeObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; 

        Image fadeImage = fadeObj.AddComponent<Image>();
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f); 

        // Memorize the exact rain volume before we start fading
        float startRainVolume = rainSound != null ? rainSound.volume : 0f;

        // 8. 💡 THE FIX 2: Fade the screen to black AND fade the rain out together!
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeDuration);
            
            // Fade the visual screen
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            
            // Fade the rain audio
            if (rainSound != null)
            {
                rainSound.volume = Mathf.Lerp(startRainVolume, 0f, alpha);
            }

            yield return null;
        }

        // Guarantee final states
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f); 
        if (rainSound != null) rainSound.volume = 0f;

        // 9. Final Pause
        yield return new WaitForSeconds(pauseBeforeReset);

        // 10. Restart Game
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}