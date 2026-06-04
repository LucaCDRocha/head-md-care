using System; 
using UnityEngine;
using UnityEngine.EventSystems;

public class Draggable : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    public Camera cam;
    
    [Tooltip("Base snapping radius. This value is now automatically scaled based on screen density so it feels identical on PC and mobile screens!")]
    public float snapDistance = 45f;
    
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

        // 2. Lock the drag depth plane directly to the target slot's depth layer
        float targetDepth = cam.WorldToScreenPoint(targetWorldPosition).z;

        // 3. Move the object along that clean target depth plane matching touch input
        Vector3 screenPos = new Vector3(eventData.position.x, eventData.position.y, targetDepth);
        transform.position = cam.ScreenToWorldPoint(screenPos);

        // 4. PIXEL DISTANCE CHECK: Measure how close they look on the 2D glass screen
        Vector2 pieceScreenPos = cam.WorldToScreenPoint(transform.position);
        Vector2 targetScreenPos = cam.WorldToScreenPoint(targetWorldPosition);

        // 5. DYNAMIC DPI FIX: Scale the required snap distance based on device pixel density.
        // We use 96 DPI as our baseline (standard PC screen). If an iPad has 264 DPI, 
        // this multiplier automatically triples the pixel radius so it matches the physical finger size!
        float screenDensityMultiplier = (Screen.dpi > 0) ? (Screen.dpi / 96f) : 1f;
        float dynamicSnapRadius = snapDistance * screenDensityMultiplier;

        if (Vector2.Distance(pieceScreenPos, targetScreenPos) <= dynamicSnapRadius)
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