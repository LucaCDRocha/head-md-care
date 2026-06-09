using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class MandatoryClick : MonoBehaviour, IPointerClickHandler
{
    [Header("Puzzle Connection")]
    public PuzzleLogic puzzleLogic;

    [Header("Cinematic Timings")]
    [Tooltip("How many seconds does the camera take to zoom in? The piece will wait this long before dropping!")]
    public float cameraTransitionTime = 2.0f; 

    // 💡 NEW: Ensures the piece only drops ONCE, without breaking the camera!
    private bool hasDroppedPiece = false; 

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
        // Ignore clicks if the coffee mug hasn't broken the vase yet
        if (puzzleLogic == null || !puzzleLogic.hasExploded) return;
        
        // 💡 THE FIX: If we already dropped the piece, just stop here. 
        // We do NOT disable the script, so the ObjectFocus script can keep zooming the camera!
        if (hasDroppedPiece) return;
        
        hasDroppedPiece = true; 
        Debug.Log($"Player tapped mandatory object: {gameObject.name}. Dropping piece in {cameraTransitionTime} seconds...");

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

        // 💡 THE FIX: The code that destroyed the collider has been completely deleted!
    }
}