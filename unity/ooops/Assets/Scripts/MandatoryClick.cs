using UnityEngine;
using UnityEngine.EventSystems;

public class MandatoryClick : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Drag the puzzle logic here so we can tell it to resume.")]
    public PuzzleLogic puzzleLogic;

    private void OnEnable()
    {
        if (puzzleLogic == null) puzzleLogic = FindAnyObjectByType<PuzzleLogic>();

        if (puzzleLogic != null)
        {
            puzzleLogic.PausePuzzle();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"Player tapped/clicked mandatory object: {gameObject.name}!");

        if (puzzleLogic != null)
        {
            // 1. Resume whatever pieces were currently active on the table
            puzzleLogic.ResumePuzzle();
            
            // 💡 2. NEW: Automatically drop the NEXT piece onto the table!
            puzzleLogic.UnlockNextPiece(); 
        }

        enabled = false; 
    }
}