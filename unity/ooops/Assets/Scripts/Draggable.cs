using System; 
using UnityEngine;
using UnityEngine.EventSystems;

// 💡 CHANGED: Swapped IPointerDown for IPointerClick!
public class Draggable : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler, IPointerClickHandler 
{
    public Camera cam;
    
    [Tooltip("Base snapping radius in screen pixels. (Try 40 to 60)")]
    public float snapDistance = 45f;
    
    [Tooltip("If true, snaps as soon as the piece touches the base object. If false, snaps when near target position.")]
    public bool useBaseObjectSnap = true;
    
    public event Action<Transform> OnPieceSnapped;

    private Vector3 originalLocalPosition;
    private bool isSnapped;
    private Vector3 dragOffset;
    private Camera boundaryCam; 
    
    private PuzzleLogic puzzleLogic; 

    public bool IsBeingDragged { get; private set; }

    private void Start()
    {
        originalLocalPosition = transform.localPosition;
        if (cam == null) cam = Camera.main;
        FindBoundaryCamera();
        
        puzzleLogic = FindAnyObjectByType<PuzzleLogic>(); 
    }

    private void FindBoundaryCamera()
    {
        GameObject dummyObj = GameObject.Find("PuzzleBoundaryAnchor_Internal");
        if (dummyObj != null) boundaryCam = dummyObj.GetComponent<Camera>();
    }

    // 💡 THE FIX: This only fires if the player TAPS the piece and releases without dragging!
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isSnapped) return; 

        // Extra safety check: If Unity accidentally registered a micro-drag, ignore the click!
        if (eventData.dragging) return; 

        if (puzzleLogic != null)
        {
            puzzleLogic.PlayPieceTapSound();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isSnapped) return;
        IsBeingDragged = true;

        if (boundaryCam == null) FindBoundaryCamera();
        Camera refCam = boundaryCam != null ? boundaryCam : cam;

        Vector3 targetWorldPosition = transform.parent.TransformPoint(originalLocalPosition);
        Plane dragPlane = new Plane(-refCam.transform.forward, targetWorldPosition);

        Ray ray = cam.ScreenPointToRay(eventData.position);
        if (dragPlane.Raycast(ray, out float enterDistance))
        {
            Vector3 hitPoint = ray.GetPoint(enterDistance);
            dragOffset = transform.position - hitPoint;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isSnapped || !IsBeingDragged) return;

        if (boundaryCam == null) FindBoundaryCamera();
        Camera refCam = boundaryCam != null ? boundaryCam : cam;

        Vector3 targetWorldPosition = transform.parent.TransformPoint(originalLocalPosition);
        Plane dragPlane = new Plane(-refCam.transform.forward, targetWorldPosition);

        Ray ray = cam.ScreenPointToRay(eventData.position);
        if (dragPlane.Raycast(ray, out float enterDistance))
        {
            transform.position = ray.GetPoint(enterDistance) + dragOffset;
        }

        bool shouldSnap = false;

        if (useBaseObjectSnap && puzzleLogic != null)
        {
            Collider pieceCollider = GetComponent<Collider>();
            Vector2 pieceScreenPos = cam.WorldToScreenPoint(transform.position);
            if (pieceCollider != null && puzzleLogic.IsTouchingBaseCollider(pieceCollider, pieceScreenPos, cam))
            {
                shouldSnap = true;
            }
        }

        if (!shouldSnap)
        {
            Vector2 pieceScreenPos = cam.WorldToScreenPoint(transform.position);
            Vector2 targetScreenPos = cam.WorldToScreenPoint(targetWorldPosition);

            float screenDensityMultiplier = (Screen.dpi > 0) ? (Screen.dpi / 96f) : 1f;
            float dynamicSnapRadius = snapDistance * screenDensityMultiplier;

            if (Vector2.Distance(pieceScreenPos, targetScreenPos) <= dynamicSnapRadius)
            {
                shouldSnap = true;
            }
        }

        if (shouldSnap)
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
    }
}