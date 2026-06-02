using UnityEngine;

public class StageManager : MonoBehaviour
{
    [Header("Puzzle Link")]
    [Tooltip("The puzzle object that has the PuzzleLogic script.")]
    public PuzzleLogic puzzleLogic;

    [System.Serializable]
    public struct PuzzleMilestone
    {
        public string milestoneName;
        public GameObject permanentPropsContainer;
        public GameObject ephemeralPropsContainer;
    }

    [Header("Progression Flow")]
    public PuzzleMilestone[] progressionSteps;

    private int currentStepIndex = -1;
    private bool isSubscribed;

    private void Awake()
    {
        ResolvePuzzleLogic();
    }

    private void OnEnable()
    {
        ResolvePuzzleLogic();

        if (puzzleLogic != null)
        {
            if (!isSubscribed)
            {
                puzzleLogic.PieceSnapped += HandlePieceSnapped;
                puzzleLogic.ObjectRestored += HandleObjectRestored;
                isSubscribed = true;
                Debug.Log("StageManager subscribed to PuzzleLogic events.");
            }
        }
        else
        {
            Debug.LogWarning("StageManager could not find a PuzzleLogic reference to subscribe to.");
        }
    }

    private void OnDisable()
    {
        if (puzzleLogic != null)
        {
            if (isSubscribed)
            {
                puzzleLogic.PieceSnapped -= HandlePieceSnapped;
                puzzleLogic.ObjectRestored -= HandleObjectRestored;
                isSubscribed = false;
            }
        }
    }

    private void ResolvePuzzleLogic()
    {
        if (puzzleLogic != null)
        {
            return;
        }

        puzzleLogic = FindAnyObjectByType<PuzzleLogic>();
    }

    private void Start()
    {
        foreach (var step in progressionSteps)
        {
            if (step.permanentPropsContainer != null)
            {
                step.permanentPropsContainer.SetActive(false);
            }

            if (step.ephemeralPropsContainer != null)
            {
                step.ephemeralPropsContainer.SetActive(false);
            }
        }
    }

    public void AdvanceStoryStep(int currentStep)
    {
        if (progressionSteps == null || progressionSteps.Length == 0)
        {
            Debug.LogWarning("StageManager has no progression steps configured.");
            return;
        }

        int clampedStep = Mathf.Clamp(currentStep, 0, progressionSteps.Length - 1);

        if (currentStepIndex >= 0 && currentStepIndex < progressionSteps.Length)
        {
            if (progressionSteps[currentStepIndex].ephemeralPropsContainer != null)
            {
                progressionSteps[currentStepIndex].ephemeralPropsContainer.SetActive(false);
            }
        }

        currentStepIndex = clampedStep;

        for (int i = 0; i <= currentStepIndex; i++)
        {
            if (i < progressionSteps.Length && progressionSteps[i].permanentPropsContainer != null)
            {
                progressionSteps[i].permanentPropsContainer.SetActive(true);
            }
        }

        if (currentStepIndex >= 0 && currentStepIndex < progressionSteps.Length)
        {
            if (progressionSteps[currentStepIndex].ephemeralPropsContainer != null)
            {
                progressionSteps[currentStepIndex].ephemeralPropsContainer.SetActive(true);
            }
        }

        if (currentStep != clampedStep)
        {
            Debug.LogWarning($"Advanced to Step {currentStepIndex} (clamped from {currentStep}). Room state updated.");
        }
        else
        {
            Debug.Log($"Advanced to Step {currentStepIndex}. Room state updated.");
        }
    }

    private void HandlePieceSnapped(Transform snappedPiece, int snappedCount, int totalPieces)
    {
        if (puzzleLogic == null || snappedPiece == null)
        {
            return;
        }

        if (snappedCount < 0 || totalPieces <= 0)
        {
            return;
        }

        Debug.Log($"Snapped piece '{snappedPiece.name}' in PuzzleLogic. Progress {snappedCount}/{totalPieces}.");
        AdvanceStoryStep(snappedCount - 1);
    }

    private void HandleObjectRestored()
    {
        Debug.Log("Puzzle completed. Final room state is active.");
    }
}