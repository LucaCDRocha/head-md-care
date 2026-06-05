using System; 
using UnityEngine;
using UnityEngine.EventSystems;

public class Draggable : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    public Camera cam;
    
    [Tooltip("Base snapping radius in screen pixels. (Try 40 to 60)")]
    public float snapDistance = 45f;
    
    public event Action<Transform> OnPieceSnapped;

    private Vector3 originalLocalPosition;
    private bool isSnapped;
    private Vector3 dragOffset;
    private Camera boundaryCam; 

    public bool IsBeingDragged { get; private set; }

    private void Start()
    {
        originalLocalPosition = transform.localPosition;
        if (cam == null) cam = Camera.main;
        FindBoundaryCamera();
    }

    private void FindBoundaryCamera()
    {
        GameObject dummyObj = GameObject.Find("PuzzleBoundaryAnchor_Internal");
        if (dummyObj != null) boundaryCam = dummyObj.GetComponent<Camera>();
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
            dragOffset = transform.position - ray.GetPoint(enterDistance);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isSnapped || cam == null) return;

        if (boundaryCam == null) FindBoundaryCamera();
        Camera refCam = boundaryCam != null ? boundaryCam : cam;

        Vector3 targetWorldPosition = transform.parent.TransformPoint(originalLocalPosition);
        Plane dragPlane = new Plane(-refCam.transform.forward, targetWorldPosition);

        Ray ray = cam.ScreenPointToRay(eventData.position);
        if (dragPlane.Raycast(ray, out float enterDistance))
        {
            transform.position = ray.GetPoint(enterDistance) + dragOffset;
        }

        // 💡 PERFECT CONSISTENCY: Back to pixel-perfect screen snapping!
        Vector2 pieceScreenPos = cam.WorldToScreenPoint(transform.position);
        Vector2 targetScreenPos = cam.WorldToScreenPoint(targetWorldPosition);

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