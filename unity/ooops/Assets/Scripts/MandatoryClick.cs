using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Cinemachine; 

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
        // Block clicks entirely if the vase is still busy exploding!
        if (puzzleLogic == null || !puzzleLogic.hasExploded || puzzleLogic.isShattering) return;
        
        // Prevent double-dropping the puzzle piece
        if (hasDroppedPiece) return;
        
        hasDroppedPiece = true; 
        Debug.Log($"Player tapped mandatory object: {gameObject.name}. Waiting for Cinemachine to finish...");

        StartCoroutine(DelayedUnlockSequence());
    }

    private IEnumerator DelayedUnlockSequence()
    {
        CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();

        if (brain != null)
        {
            yield return null; 
            yield return new WaitWhile(() => brain.IsBlending);
        }
        else
        {
            yield return new WaitForSeconds(2.0f);
        }

        if (puzzleLogic != null)
        {
            puzzleLogic.ResumePuzzle();
            puzzleLogic.UnlockNextPiece(); 
        }
    }
}