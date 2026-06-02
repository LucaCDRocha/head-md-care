using UnityEngine;

public class StageManager : MonoBehaviour
{
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
        if (currentStepIndex >= 0 && currentStepIndex < progressionSteps.Length)
        {
            if (progressionSteps[currentStepIndex].ephemeralPropsContainer != null)
            {
                progressionSteps[currentStepIndex].ephemeralPropsContainer.SetActive(false);
            }
        }

        currentStepIndex = currentStep;

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

        Debug.Log($"Advanced to Step {currentStepIndex}. Room state updated.");
    }
}