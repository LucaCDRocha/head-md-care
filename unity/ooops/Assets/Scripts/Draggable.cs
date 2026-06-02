using UnityEngine;
using UnityEngine.EventSystems;

public class Draggable : MonoBehaviour, IDragHandler
{
    // take a camera in parameter
    public Camera cam;
    [Tooltip("Distance from the original position at which the shard snaps into place.")]
    public float snapDistance = 0.2f;
    [Tooltip("Optional reference to the puzzle logic script used to notify snap progress.")]
    public PuzzleLogic puzzleLogic;

    private Vector3 originalWorldPosition;
    private bool isSnapped;

    private void Start()
    {
        originalWorldPosition = transform.position;

        if (puzzleLogic == null)
        {
            puzzleLogic = GetComponentInParent<PuzzleLogic>();
        }
    }

    // on drag, move the object to the position of the mouse
    public void OnDrag(PointerEventData eventData)
    {
        if (isSnapped)
        {
            return;
        }

        var cam = this.cam;
        if (cam == null) return;

        if (Vector3.Distance(transform.position, originalWorldPosition) <= snapDistance)
        {
            SnapToOriginalPosition();
            return;
        }

        // move object to mouse position while preserving its screen-space Z
        transform.position = cam.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y,
            cam.WorldToScreenPoint(transform.position).z));

        if (Vector3.Distance(transform.position, originalWorldPosition) <= snapDistance)
        {
            SnapToOriginalPosition();
        }
    }

    private void SnapToOriginalPosition()
    {
        isSnapped = true;
        transform.position = originalWorldPosition;

        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        if (puzzleLogic != null)
        {
            puzzleLogic.RegisterPieceSnap(transform);
        }

        enabled = false;
    }


}
