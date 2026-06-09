using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine; // 💡 NEW: Needed to talk to the Cinemachine Brain!

public class MandatoryClick : MonoBehaviour, IPointerClickHandler
{
    [Header("Puzzle Connection")]
    public PuzzleLogic puzzleLogic;

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
        if (puzzleLogic == null || !puzzleLogic.hasExploded) return;
        if (hasDroppedPiece) return;
        
        hasDroppedPiece = true; 
        Debug.Log($"Player tapped mandatory object: {gameObject.name}. Waiting for Cinemachine to finish...");

        StartCoroutine(DelayedUnlockSequence());
    }

    private IEnumerator DelayedUnlockSequence()
    {
        // 1. Find the Cinemachine Brain on the Main Camera
        CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();

        if (brain != null)
        {
            // Wait exactly 1 frame to give Cinemachine time to start the transition
            yield return null; 

            // 💡 THE FIX: Automatically wait until the camera physically stops moving!
            yield return new WaitWhile(() => brain.IsBlending);
        }
        else
        {
            // Fallback just in case you accidentally delete the brain!
            yield return new WaitForSeconds(2.0f);
        }

        // 2. Resume the puzzle and drop the piece!
        if (puzzleLogic != null)
        {
            puzzleLogic.ResumePuzzle();
            puzzleLogic.UnlockNextPiece(); 
        }
    }
}