using UnityEngine;
using UnityEngine.EventSystems;

public class MandatoryClick : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Drag the puzzle logic here so we can tell it to resume. Will auto-find if left empty.")]
    public PuzzleLogic puzzleLogic;

    private void OnEnable()
    {
        // Fallback: If not assigned in the inspector, find it automatically
        if (puzzleLogic == null)
        {
            puzzleLogic = FindAnyObjectByType<PuzzleLogic>();
        }

        // As soon as StageManager activates this object, pause the puzzle!
        if (puzzleLogic != null)
        {
            puzzleLogic.PausePuzzle();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"Player clicked {gameObject.name}!");

        // 1. Tell the puzzle to start up again
        if (puzzleLogic != null)
        {
            puzzleLogic.ResumePuzzle();
        }

        // 2. Do whatever else this object is supposed to do (play a sound, open a drawer, etc.)
        
        // 3. Turn off this object's interaction so they can't click it again
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }
}