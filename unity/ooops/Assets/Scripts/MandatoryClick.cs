using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class MandatoryClick : MonoBehaviour, IPointerClickHandler
{
    [Header("Puzzle Connection")]
    public PuzzleLogic puzzleLogic;

    [Header("Cinematic Timings")]
    [Tooltip("How many seconds does the camera take to zoom in? The piece will wait this long before dropping!")]
    public float cameraTransitionTime = 2.0f; // 💡 NEW: The delay timer!

    private bool hasTriggered = false;

    private void OnEnable()
    {
        if (puzzleLogic == null) puzzleLogic = FindAnyObjectByType<PuzzleLogic>();

        if (puzzleLogic != null && puzzleLogic.hasExploded)
        {
            puzzleLogic.PausePuzzle();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Ignore clicks if the coffee mug hasn't broken the vase yet, or if already clicked
        if (hasTriggered || puzzleLogic == null || !puzzleLogic.hasExploded) return;
        
        hasTriggered = true; // Lock it so they can't spam click it!
        Debug.Log($"Player tapped mandatory object: {gameObject.name}. Waiting for camera...");

        // 💡 NEW: Start the delayed sequence instead of unlocking instantly
        StartCoroutine(DelayedUnlockSequence());
    }

    private IEnumerator DelayedUnlockSequence()
    {
        // 1. Wait for the Cinemachine camera to finish moving
        yield return new WaitForSeconds(cameraTransitionTime);

        // 2. Resume the puzzle and drop the piece!
        if (puzzleLogic != null)
        {
            puzzleLogic.ResumePuzzle();
            puzzleLogic.UnlockNextPiece(); 
        }

        // 3. Turn off the collider so this object can never be clicked again
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        
        enabled = false; 
    }
}