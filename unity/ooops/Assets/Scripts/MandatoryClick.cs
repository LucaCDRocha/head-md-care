using UnityEngine;
using UnityEngine.EventSystems; // Required for IPointerClickHandler

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

    // Works seamlessly via Unity EventSystem on both PC Mouse and Tablet Taps!
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"Player tapped/clicked mandatory object: {gameObject.name}!");

        // 1. Resume the puzzle logic shards
        if (puzzleLogic != null)
        {
            puzzleLogic.ResumePuzzle();
        }

        // 2. Turn off ONLY this script component so it can never pause the puzzle again, 
        // but leave the physical Collider enabled so ObjectFocus can still zoom in on it!
        enabled = false; 
    }
}