using UnityEngine;
using UnityEngine.EventSystems;

public class InputManagerView : MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
    // IPointerMoveHandler
{
  [SerializeField] private GunManager gunManager;

  public void OnPointerDown(PointerEventData eventData)
  {
    gunManager.UpdateAim(eventData.position);
    gunManager.SetFiring(true);
  }

  public void OnDrag(PointerEventData eventData)
  {
    gunManager.UpdateAim(eventData.position);
  }

  public void OnPointerUp(PointerEventData eventData)
  {
    gunManager.SetFiring(false);
  }

  // public void OnPointerMove(PointerEventData eventData)
  // {
  //   gunManager.UpdateAim(eventData.position);
  // }
}
