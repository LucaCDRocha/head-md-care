using UnityEngine;
using UnityEngine.EventSystems;

public class Draggable : MonoBehaviour, IDragHandler
{
    // take a camera in parameter
    public Camera cam;
    // on drag, move the object to the position of the mouse
    public void OnDrag(PointerEventData eventData)
    {
        var cam = this.cam;
        if (cam == null) return;
        // move object to mouse position while preserving its screen-space Z
        transform.position = cam.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y,
            cam.WorldToScreenPoint(transform.position).z));
    }


}
