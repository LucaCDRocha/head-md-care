using System; // <-- Add this for Action
using UnityEngine;
using UnityEngine.EventSystems;

public class Draggable : MonoBehaviour, IDragHandler
{
    public Camera cam;
    public float snapDistance = 0.2f;
    
    // REMOVED: public PuzzleLogic puzzleLogic;
    
    // ADDED: An event that broadcasts when this specific piece snaps
    public event Action<Transform> OnPieceSnapped;

    private Vector3 originalLocalPosition;
    private bool isSnapped;

    private void Start()
    {
        originalLocalPosition = transform.localPosition;
        if (cam == null) cam = Camera.main;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isSnapped || cam == null) return;

        Vector3 targetWorldPosition = transform.parent.TransformPoint(originalLocalPosition);

        if (Vector3.Distance(transform.position, targetWorldPosition) <= snapDistance)
        {
            SnapToOriginalPosition(targetWorldPosition);
            return;
        }

        Vector3 screenPos = new Vector3(eventData.position.x, eventData.position.y, cam.WorldToScreenPoint(transform.position).z);
        transform.position = cam.ScreenToWorldPoint(screenPos);

        if (Vector3.Distance(transform.position, targetWorldPosition) <= snapDistance)
        {
            SnapToOriginalPosition(targetWorldPosition);
        }
    }

    private void SnapToOriginalPosition(Vector3 targetWorldPos)
    {
        isSnapped = true;
        transform.position = targetWorldPos;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // ADDED: Shout into the void that we snapped!
        OnPieceSnapped?.Invoke(transform);

        enabled = false;
    }
}