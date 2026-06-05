using System; 
using UnityEngine;
using UnityEngine.EventSystems;

public class Draggable : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    public Camera cam;
    public float snapDistance = 45f;
    public event Action<Transform> OnPieceSnapped;

    private Vector3 originalLocalPosition;
    private bool isSnapped;
    private Vector3 dragOffset;

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

        Vector3 targetWorldPosition = transform.parent.TransformPoint(originalLocalPosition);
        
        // 💡 THE SCREEN-FLAT FIX: 
        // We force the dragging plane to perfectly face the camera (2D screen), 
        // but we anchor its depth exactly at the target 3D puzzle slot!
        Plane dragPlane = new Plane(-cam.transform.forward, targetWorldPosition);

        Ray ray = cam.ScreenPointToRay(eventData.position);
        if (dragPlane.Raycast(ray, out float enterDistance))
        {
            Vector3 hitPoint = ray.GetPoint(enterDistance);
            dragOffset = transform.position - hitPoint;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isSnapped || cam == null) return;

        Vector3 targetWorldPosition = transform.parent.TransformPoint(originalLocalPosition);
        Plane dragPlane = new Plane(-cam.transform.forward, targetWorldPosition);

        Ray ray = cam.ScreenPointToRay(eventData.position);
        if (dragPlane.Raycast(ray, out float enterDistance))
        {
            Vector3 hitPoint = ray.GetPoint(enterDistance);
            
            // The piece now drags perfectly 1-to-1 with your finger across the iPad!
            transform.position = hitPoint + dragOffset;
        }

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