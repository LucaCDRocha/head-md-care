using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class ExhibitionManager : MonoBehaviour
{
    private static ExhibitionManager instance;
    public static ExhibitionManager Instance => instance;

    [Header("Inactivity Timer Settings")]
    [Tooltip("Time in seconds of player inactivity before resetting the game automatically.")]
    public float idleTimeoutSeconds = 60f;

    [Tooltip("Master switch to enable or disable the inactivity timer.")]
    public bool enableInactivityTimer = true;

    [Header("4 Corners Reset Settings")]
    [Tooltip("Master switch to enable or disable the 4-corner reset gesture.")]
    public bool enableFourCornerReset = true;

    [Tooltip("Corner region size as a fraction of screen dimensions (0.15 = 15% of width/height).")]
    [Range(0.05f, 0.35f)]
    public float cornerSizeRatio = 0.15f;

    [Tooltip("Time window in seconds within which all 4 corners must be clicked/tapped (for single pointer / mouse).")]
    public float cornerMultiTapWindow = 1.5f;

    [Tooltip("If true, pressing 'R' on keyboard will trigger instant game reset (for testing/development).")]
    public bool enableDebugKeyboardReset = true;

    [Header("Debug Info (Read-Only)")]
    [SerializeField] private float timeSinceLastInteraction = 0f;
    [SerializeField] private bool isObjectFocusAudioPlaying = false;
    [SerializeField] private bool isTimerPausedForAudio = false;

    public float TimeSinceLastInteraction => timeSinceLastInteraction;
    public bool IsObjectFocusAudioPlaying => isObjectFocusAudioPlaying;
    public bool IsTimerPausedForAudio => isTimerPausedForAudio;

    private bool wasAudioPlaying = false;

    // Multi-tap timestamps for corners (0: Top-Left, 1: Top-Right, 2: Bottom-Left, 3: Bottom-Right)
    private float[] cornerTapTimes = new float[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (FindAnyObjectByType<ExhibitionManager>() == null)
        {
            GameObject managerObject = new GameObject("[ExhibitionManager]");
            managerObject.AddComponent<ExhibitionManager>();
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        // Don't run idle timeout reset while the game is already in the final endgame sequence
        if (EndGameSequence.IsEnding)
        {
            timeSinceLastInteraction = 0f;
            return;
        }

        if (enableFourCornerReset)
        {
            CheckFourCornerReset();
        }

        // Don't run inactivity timer if the game has not started yet (intro/menu screen)
        if (!StartGame.IsGameStarted)
        {
            timeSinceLastInteraction = 0f;
            return;
        }

        if (enableInactivityTimer)
        {
            UpdateInactivityTimer();
        }
    }

    private void UpdateInactivityTimer()
    {
        // Check if an inspection audio is currently playing in an ObjectFocus script
        isObjectFocusAudioPlaying = ObjectFocus.IsAnyObjectFocusAudioPlaying;

        if (isObjectFocusAudioPlaying)
        {
            // Audio is playing in an ObjectFocus script: STOP / PAUSE the timer
            isTimerPausedForAudio = true;
            wasAudioPlaying = true;
            
            // Touch/click during audio still resets interaction timer so when audio stops, full timeout starts
            if (DetectAnyUserInteraction())
            {
                timeSinceLastInteraction = 0f;
            }
            return;
        }

        // Audio is not currently playing.
        // If audio WAS playing in previous frame, reset the timer to 0 now that audio finished or was stopped!
        if (wasAudioPlaying)
        {
            timeSinceLastInteraction = 0f;
            wasAudioPlaying = false;
            isTimerPausedForAudio = false;
        }

        // Check for user interaction (screen touch, mouse click, key press)
        if (DetectAnyUserInteraction())
        {
            timeSinceLastInteraction = 0f;
        }
        else
        {
            timeSinceLastInteraction += Time.deltaTime;

            if (timeSinceLastInteraction >= idleTimeoutSeconds)
            {
                Debug.Log($"[ExhibitionManager] Inactivity timeout of {idleTimeoutSeconds}s reached. Resetting game...");
                ResetGame();
            }
        }
    }

    private bool DetectAnyUserInteraction()
    {
        // 1. Check Touchscreen
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.isInProgress) return true;
            }
        }

        // 2. Check Pointer (Mouse / Touch / Pen)
        if (Pointer.current != null)
        {
            if (Pointer.current.press.wasPressedThisFrame || 
                (Pointer.current.wasUpdatedThisFrame && Pointer.current.press.isPressed))
            {
                return true;
            }
        }

        // 3. Check Keyboard
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private void CheckFourCornerReset()
    {
        // Debug key shortcut ('R' key)
        if (enableDebugKeyboardReset)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                Debug.Log("[ExhibitionManager] Debug key 'R' pressed. Resetting game...");
                ResetGame();
                return;
            }
        }

        List<Vector2> currentTouchPositions = GetCurrentTouchPositions();

        if (currentTouchPositions.Count == 0) return;

        bool hasTL = false;
        bool hasTR = false;
        bool hasBL = false;
        bool hasBR = false;

        float screenW = Screen.width;
        float screenH = Screen.height;

        foreach (Vector2 pos in currentTouchPositions)
        {
            int cornerIndex = GetCornerIndex(pos, screenW, screenH, cornerSizeRatio);
            if (cornerIndex == 0) { hasTL = true; cornerTapTimes[0] = Time.time; }
            else if (cornerIndex == 1) { hasTR = true; cornerTapTimes[1] = Time.time; }
            else if (cornerIndex == 2) { hasBL = true; cornerTapTimes[2] = Time.time; }
            else if (cornerIndex == 3) { hasBR = true; cornerTapTimes[3] = Time.time; }
        }

        // 1. Simultaneous multi-touch check (all 4 corners pressed at once)
        if (hasTL && hasTR && hasBL && hasBR)
        {
            Debug.Log("[ExhibitionManager] Simultaneous 4-corner touch detected! Resetting game...");
            ResetGame();
            return;
        }

        // 2. Fast multi-tap sequence check (all 4 corners touched within cornerMultiTapWindow)
        float now = Time.time;
        if (now - cornerTapTimes[0] <= cornerMultiTapWindow &&
            now - cornerTapTimes[1] <= cornerMultiTapWindow &&
            now - cornerTapTimes[2] <= cornerMultiTapWindow &&
            now - cornerTapTimes[3] <= cornerMultiTapWindow &&
            cornerTapTimes[0] > 0 && cornerTapTimes[1] > 0 && cornerTapTimes[2] > 0 && cornerTapTimes[3] > 0)
        {
            Debug.Log("[ExhibitionManager] 4-corner multi-tap sequence detected! Resetting game...");
            for (int i = 0; i < 4; i++) cornerTapTimes[i] = 0f;
            ResetGame();
            return;
        }
    }

    private int GetCornerIndex(Vector2 pos, float screenW, float screenH, float ratio)
    {
        bool isLeft = pos.x <= screenW * ratio;
        bool isRight = pos.x >= screenW * (1f - ratio);
        bool isBottom = pos.y <= screenH * ratio;
        bool isTop = pos.y >= screenH * (1f - ratio);

        if (isLeft && isTop) return 0;     // Top-Left
        if (isRight && isTop) return 1;    // Top-Right
        if (isLeft && isBottom) return 2;  // Bottom-Left
        if (isRight && isBottom) return 3; // Bottom-Right

        return -1;
    }

    private List<Vector2> GetCurrentTouchPositions()
    {
        List<Vector2> positions = new List<Vector2>();

        // Touchscreen active touches
        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.isInProgress)
                {
                    Vector2 pos = touch.position.ReadValue();
                    if (!positions.Contains(pos))
                    {
                        positions.Add(pos);
                    }
                }
            }
        }

        // Single Pointer (Mouse / Touch / Pen) when pressed
        if (positions.Count == 0 && Pointer.current != null && Pointer.current.press.isPressed)
        {
            Vector2 pos = Pointer.current.position.ReadValue();
            positions.Add(pos);
        }

        return positions;
    }

    public void ResetGame()
    {
        timeSinceLastInteraction = 0f;
        wasAudioPlaying = false;
        isTimerPausedForAudio = false;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
