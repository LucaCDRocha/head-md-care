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

    // --- AXIS LOCK TRACKING VARIABLES ---
    private Vector3 dragOffset;
    private Camera boundaryCam;

    // Flag so PuzzleLogic knows when to let go of physics updates
    public bool IsBeingDragged { get; private set; }

    private void Start()
    {
        originalLocalPosition = transform.localPosition;
        if (cam == null) cam = Camera.main;
        FindBoundaryCamera();
    }

    private void FindBoundaryCamera()
    {
        // Finds the tracking anchor created dynamically by PuzzleLogic
        GameObject dummyObj = GameObject.Find("PuzzleBoundaryAnchor_Internal");
        if (dummyObj != null)
        {
            boundaryCam = dummyObj.GetComponent<Camera>();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isSnapped) return;
        IsBeingDragged = true;

        if (boundaryCam == null) FindBoundaryCamera();

        Vector3 targetWorldPosition = transform.parent.TransformPoint(originalLocalPosition);
        
        // 💡 THE CRITICAL FIX: We add a minus (-) sign here! 
        // This faces the plane TOWARDS the camera so the touch raycast never fails.
        Vector3 planeNormal = (boundaryCam != null) ? -boundaryCam.transform.forward : -cam.transform.forward;
        Plane dragPlane = new Plane(planeNormal, targetWorldPosition);

        Ray ray = cam.ScreenPointToRay(eventData.position);
        if (dragPlane.Raycast(ray, out float enterDistance))
        {
            Vector3 hitPoint = ray.GetPoint(enterDistance);
            dragOffset = transform.position - hitPoint; // Smooth offset tracking
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isSnapped || cam == null) return;

        if (boundaryCam == null) FindBoundaryCamera();

        Vector3 targetWorldPosition = transform.parent.TransformPoint(originalLocalPosition);

        // 💡 THE CRITICAL FIX: Face the plane towards the camera here as well.
        Vector3 planeNormal = (boundaryCam != null) ? -boundaryCam.transform.forward : -cam.transform.forward;
        Plane dragPlane = new Plane(planeNormal, targetWorldPosition);

        Ray ray = cam.ScreenPointToRay(eventData.position);
        if (dragPlane.Raycast(ray, out float enterDistance))
        {
            Vector3 hitPoint = ray.GetPoint(enterDistance);
            
            // Move the piece perfectly along the physics floating grid plane
            transform.position = hitPoint + dragOffset;
        }

        // Measure how close they look on the 2D glass screen
        Vector2 pieceScreenPos = cam.WorldToScreenPoint(transform.position);
        Vector2 targetScreenPos = cam.WorldToScreenPoint(targetWorldPosition);

        // Scale the required snap distance based on device pixel density.
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