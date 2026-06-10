using System.Collections;
using UnityEngine;
using TMPro; 

public class CalendarTimeController : MonoBehaviour
{
    [Header("Connections")]
    public PuzzleLogic puzzleLogic;
    public Transform rotationPivot;

    [Header("3D Dynamic Text")]
    public TextMeshPro calendarTextField; 
    
    [Header("Custom Calendar Steps")]
    [Tooltip("Type your strings here containing the years (e.g., '2026', '2021', '2023', etc.)")]
    public string[] calendarSteps = new string[] { "2026", "2021", "2023", "2024" };

    [Header("Rewind Settings (Shatter Phase)")]
    public float rewindSpinSpeed = 720f; 

    [Header("Progression Settings (Snap Phase)")]
    public float snapFlipDegrees = -360f; 
    [Tooltip("How long ONE single page flip takes.")]
    public float snapFlipDuration = 0.3f; 

    private Coroutine activeAnimation;
    private Quaternion originalRotation;
    private int currentStepIndex = 0; 

    private void Start()
    {
        currentStepIndex = 0; 
        UpdateCalendarText();

        if (rotationPivot != null)
        {
            originalRotation = rotationPivot.localRotation;
        }

        if (puzzleLogic == null) puzzleLogic = FindAnyObjectByType<PuzzleLogic>();
    }

    private void OnEnable()
    {
        if (puzzleLogic == null) puzzleLogic = FindAnyObjectByType<PuzzleLogic>();
        
        if (puzzleLogic != null)
        {
            puzzleLogic.PuzzleExploded += StartRewindSpin;
            puzzleLogic.PieceSnapped += HandlePieceSnap;
        }
    }

    private void OnDisable()
    {
        if (puzzleLogic != null)
        {
            puzzleLogic.PuzzleExploded -= StartRewindSpin;
            puzzleLogic.PieceSnapped -= HandlePieceSnap;
        }
    }

    // --- PHASE 1: FRANTIC REWIND ---
    private void StartRewindSpin()
    {
        if (activeAnimation != null) StopCoroutine(activeAnimation);
        activeAnimation = StartCoroutine(RewindSpinRoutine());
    }

    private IEnumerator RewindSpinRoutine()
    {
        // 💡 NEW: Extract the start year and end year to calculate a digital countdown loop
        int startYear = ExtractYearFromString(calendarSteps[0]);
        int endYear = ExtractYearFromString(calendarSteps[1]);
        
        // Match the countdown timing perfectly to the vase explosion duration
        float totalDuration = puzzleLogic != null ? puzzleLogic.fallDuration : 1.5f;
        float elapsed = 0f;

        while (puzzleLogic != null && puzzleLogic.isShattering)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / totalDuration);
            
            // 💡 NEW: Calculate the exact descending value frame-by-frame
            int currentRewindYear = Mathf.RoundToInt(Mathf.Lerp(startYear, endYear, t));
            
            if (calendarTextField != null)
            {
                calendarTextField.text = currentRewindYear.ToString();
            }

            rotationPivot.Rotate(Vector3.right, -rewindSpinSpeed * Time.deltaTime, Space.Self);
            yield return null;
        }

        if (rotationPivot != null)
        {
            rotationPivot.localRotation = originalRotation;
        }

        currentStepIndex = 1; 
        UpdateCalendarText(); // Solidify the final text structure layout configuration
    }

    // --- PHASE 2: PIECE SNAP FLIP ---
    private void HandlePieceSnap(Transform piece, int snappedCount, int totalPieces)
    {
        int oldIndex = currentStepIndex;
        int newIndex = Mathf.Min(currentStepIndex + 1, calendarSteps.Length - 1);

        int flipCount = CalculateYearDifference(oldIndex, newIndex);
        currentStepIndex = newIndex;

        if (activeAnimation != null) StopCoroutine(activeAnimation);
        activeAnimation = StartCoroutine(SnapFlipRoutine(flipCount));
    }

    private IEnumerator SnapFlipRoutine(int totalFlips)
    {
        for (int f = 0; f < totalFlips; f++)
        {
            float elapsed = 0f;
            Quaternion loopStartRot = rotationPivot.localRotation;

            // (Notice we completely removed the halfway text swap logic block from inside this loop!)
            while (elapsed < snapFlipDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / snapFlipDuration);
                float smoothT = t * t * (3f - 2f * t); 

                float currentAngle = smoothT * snapFlipDegrees;
                rotationPivot.localRotation = loopStartRot * Quaternion.Euler(currentAngle, 0, 0);

                yield return null;
            }

            rotationPivot.localRotation = loopStartRot * Quaternion.Euler(snapFlipDegrees, 0, 0);
        }

        // 💡 THE FIX: The text now switches ONLY after all loops have completely finished moving!
        UpdateCalendarText();
    }

    private int CalculateYearDifference(int indexA, int indexB)
    {
        if (calendarSteps == null || indexA >= calendarSteps.Length || indexB >= calendarSteps.Length) return 1;

        int yearA = ExtractYearFromString(calendarSteps[indexA]);
        int yearB = ExtractYearFromString(calendarSteps[indexB]);

        int difference = Mathf.Abs(yearB - yearA);
        return difference == 0 ? 1 : difference;
    }

    private int ExtractYearFromString(string text)
    {
        string numbersOnly = "";
        foreach (char c in text)
        {
            if (char.IsDigit(c)) numbersOnly += c;
        }

        if (int.TryParse(numbersOnly, out int parsedYear))
        {
            return parsedYear;
        }
        return 0; 
    }

    private void UpdateCalendarText()
    {
        if (calendarTextField == null) return;

        if (calendarSteps != null && calendarSteps.Length > 0)
        {
            int safeIndex = Mathf.Clamp(currentStepIndex, 0, calendarSteps.Length - 1);
            calendarTextField.text = calendarSteps[safeIndex];
        }
    }

    public void ResetCalendar()
    {
        if (activeAnimation != null) StopCoroutine(activeAnimation);
        if (rotationPivot != null) rotationPivot.localRotation = originalRotation;
        
        currentStepIndex = 0;
        UpdateCalendarText();
    }
}