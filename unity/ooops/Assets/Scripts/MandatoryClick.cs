using UnityEngine;
using UnityEngine.EventSystems;

public class MandatoryClick : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Drag the puzzle logic here so we can tell it to resume.")]
    public PuzzleLogic puzzleLogic;

    private void OnEnable()
    {
        if (puzzleLogic == null) puzzleLogic = FindAnyObjectByType<PuzzleLogic>();

        // 💡 BUG FIX: Only pause the puzzle if it has actually started!
        if (puzzleLogic != null && puzzleLogic.hasExploded)
        {
            puzzleLogic.PausePuzzle();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 💡 BUG FIX: Ignore clicks if the coffee mug hasn't been clicked yet!
        if (puzzleLogic != null && !puzzleLogic.hasExploded) return;

        Debug.Log($"Player tapped/clicked mandatory object: {gameObject.name}!");

        if (puzzleLogic != null)
        {
            puzzleLogic.ResumePuzzle();
            puzzleLogic.UnlockNextPiece(); 
        }

        enabled = false; 
    }
}