using System;
using System.Collections; // 💡 ADD THIS LINE RIGHT HERE!
using System.Collections.Generic;
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

    [Header("Animation Settings")]
    [Tooltip("Time in seconds for objects to fully pop in or out.")]
    public float animationDuration = 0.35f;

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
                StartCoroutine(PopOutCoroutine(progressionSteps[currentStepIndex].ephemeralPropsContainer));
            }
        }

        currentStepIndex = clampedStep;

        for (int i = 0; i <= currentStepIndex; i++)
        {
            if (i < progressionSteps.Length && progressionSteps[i].permanentPropsContainer != null)
            {
                StartCoroutine(PopInCoroutine(progressionSteps[i].permanentPropsContainer));
            }
        }

        if (currentStepIndex >= 0 && currentStepIndex < progressionSteps.Length)
        {
            if (progressionSteps[currentStepIndex].ephemeralPropsContainer != null)
            {
                StartCoroutine(PopInCoroutine(progressionSteps[currentStepIndex].ephemeralPropsContainer));
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

    // --- DROP-IN ANIMATION RECIPES ---
    // --- AUTOMATIC SCALE MEMORY BANK ---
    private System.Collections.Generic.Dictionary<GameObject, Vector3> nativeScales = new System.Collections.Generic.Dictionary<GameObject, Vector3>();

    private IEnumerator PopInCoroutine(GameObject obj, float customDuration = -1f)
    {
        if (obj == null) yield break;
        float finalDuration = customDuration > 0f ? customDuration : animationDuration;

        // 1. Gather all the child elements inside this entity (excluding the parent itself)
        System.Collections.Generic.List<Transform> targets = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in obj.GetComponentsInChildren<Transform>(true))
        {
            if (t.gameObject != obj) targets.Add(t);
        }

        // Fallback: If it's a lone object with no children, just animate the object itself
        if (targets.Count == 0) targets.Add(obj.transform);

        // 2. Automatically memorize the true native scale of every individual child
        foreach (Transform t in targets)
        {
            if (!nativeScales.ContainsKey(t.gameObject))
            {
                nativeScales[t.gameObject] = t.localScale;
            }
        }

        // 💡 GUARD CLAUSE: If the parent is already on and the first child is already full size, skip entirely
        if (obj.activeSelf && targets[0].localScale == nativeScales[targets[0].gameObject])
        {
            yield break;
        }

        // 3. Flatten every child to zero right before making the parent group visible
        foreach (Transform t in targets)
        {
            if (t != null) t.localScale = Vector3.zero;
        }
        obj.SetActive(true);

        // 4. Smoothly inflate every child simultaneously from their own individual pivots
        float elapsed = 0f;
        while (elapsed < finalDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / finalDuration);
            float smoothT = t * t * (3f - 2f * t); // Cartoon easing curve

            foreach (Transform target in targets)
            {
                if (target != null)
                {
                    target.localScale = Vector3.Lerp(Vector3.zero, nativeScales[target.gameObject], smoothT);
                }
            }
            yield return null;
        }

        // Ensure everything finishes perfectly at its exact native size
        foreach (Transform target in targets)
        {
            if (target != null) target.localScale = nativeScales[target.gameObject];
        }
    }

    private IEnumerator PopOutCoroutine(GameObject obj, float customDuration = -1f)
    {
        if (obj == null || !obj.activeSelf) yield break;
        float finalDuration = customDuration > 0f ? customDuration : animationDuration;

        // 1. Gather all the child elements inside this entity
        System.Collections.Generic.List<Transform> targets = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in obj.GetComponentsInChildren<Transform>(true))
        {
            if (t.gameObject != obj) targets.Add(t);
        }
        if (targets.Count == 0) targets.Add(obj.transform);

        // 2. Remember scales and track their current starting positions to shrink down from cleanly
        System.Collections.Generic.Dictionary<Transform, Vector3> startScales = new System.Collections.Generic.Dictionary<Transform, Vector3>();
        foreach (Transform t in targets)
        {
            if (t == null) continue;
            if (!nativeScales.ContainsKey(t.gameObject)) nativeScales[t.gameObject] = t.localScale;
            startScales[t] = t.localScale;
        }

        // 3. Shrink all children down into their own individual centers
        float elapsed = 0f;
        while (elapsed < finalDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / finalDuration);
            float smoothT = t * t * (3f - 2f * t);

            foreach (Transform target in targets)
            {
                if (target != null && startScales.ContainsKey(target))
                {
                    target.localScale = Vector3.Lerp(startScales[target], Vector3.zero, smoothT);
                }
            }
            yield return null;
        }

        // 4. Hide the entire parent group once all children are safely flattened
        foreach (Transform target in targets)
        {
            if (target != null) target.localScale = Vector3.zero;
        }
        obj.SetActive(false);
    }
}