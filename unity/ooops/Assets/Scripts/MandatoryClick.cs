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

        // Block clicks if the camera is currently transitioning to prevent any weird timing issues with Cinemachine blends and puzzle state changes.
        if (ObjectFocus.isTransitioning) return;

        // Block clicks during unfocus lockout window!
        if (Time.time < ObjectFocus.ignoreFocusClicksUntil) return;

        // Block mandatory clicks if ANY object is currently focused in the focus camera!
        if (ObjectFocus.IsAnyObjectFocused)
        {
            if (ObjectFocus.CurrentlyFocusedObject != null)
            {
                ObjectFocus.CurrentlyFocusedObject.Unfocus();
            }
            else
            {
                ObjectFocus.ignoreFocusClicksUntil = Time.time + 0.5f;
            }
            return;
        }

        // Prevent double-dropping the puzzle piece
        if (hasDroppedPiece) return;

        hasDroppedPiece = true;
        Debug.Log($"Player tapped mandatory object: {gameObject.name}. Waiting for Cinemachine to finish...");

        StartCoroutine(DelayedUnlockSequence());
    }

    private IEnumerator DelayedUnlockSequence()
    {
        CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();

        // Yield a frame so ObjectFocus.OnPointerClick can initiate transition & update focus state if present
        yield return null;

        if (brain != null)
        {
            yield return new WaitWhile(() => brain.IsBlending);
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }

        // If an ObjectFocus script is attached to this object (or parent/child), wait for the focus inspection to complete & unfocus
        ObjectFocus focus = GetComponent<ObjectFocus>();
        if (focus == null) focus = GetComponentInParent<ObjectFocus>();
        if (focus == null) focus = GetComponentInChildren<ObjectFocus>();

        if (focus != null)
        {
            yield return new WaitWhile(() => ObjectFocus.CurrentlyFocusedObject == focus || ObjectFocus.isTransitioning || ObjectFocus.IsAnyObjectFocused);

            if (brain != null && brain.IsBlending)
            {
                yield return new WaitWhile(() => brain.IsBlending);
            }
        }

        if (puzzleLogic != null)
        {
            puzzleLogic.ResumePuzzle();
            puzzleLogic.UnlockNextPiece();
        }
    }
}