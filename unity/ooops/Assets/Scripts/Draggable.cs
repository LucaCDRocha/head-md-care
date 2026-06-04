using System; 
using UnityEngine;
using UnityEngine.EventSystems;

public class Draggable : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    public Camera cam;
    
    [Tooltip("Snapping radius measured in screen pixels (e.g., 40-50 pixels). Always feels perfectly uniform!")]
    public float snapDistance = 40f;
    
    public event Action<Transform> OnPieceSnapped;

    private Vector3 originalLocalPosition;
    private bool isSnapped;

    // Flag so PuzzleLogic knows when to let go of physics updates
    public bool IsBeingDragged { get; private set; }

    private void Start()
    {
        originalLocalPosition = transform.localPosition;
        if (cam == null) cam = Camera.main;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isSnapped) return;
        IsBeingDragged = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isSnapped || cam == null) return;

        // 1. Find exactly where this piece belongs in world space
        Vector3 targetWorldPosition = transform.parent.TransformPoint(originalLocalPosition);

        // 2. CRITICAL FIX: Lock the drag depth plane directly to the target slot's depth layer
        // This stops pieces from warp-shifting closer or further from the camera lens while dragging
        float targetDepth = cam.WorldToScreenPoint(targetWorldPosition).z;

        // 3. Move the object along that clean target depth plane matching touch input
        Vector3 screenPos = new Vector3(eventData.position.x, eventData.position.y, targetDepth);
        transform.position = cam.ScreenToWorldPoint(screenPos);

        // 4. PIXEL DISTANCE CHECK: Measure how close they look on the 2D glass screen
        Vector2 pieceScreenPos = cam.WorldToScreenPoint(transform.position);
        Vector2 targetScreenPos = cam.WorldToScreenPoint(targetWorldPosition);

        if (Vector2.Distance(pieceScreenPos, targetScreenPos) <= snapDistance)
        {
            SnapToOriginalPosition(targetWorldPosition);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        IsBeingDragged = false;
    }

    private void SnapToOriginalPosition(Vector3 targetWorldPos)
    {
        isSnapped = true;
        IsBeingDragged = false;
        transform.position = targetWorldPos;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        OnPieceSnapped?.Invoke(transform);
        enabled = false;
    }
}