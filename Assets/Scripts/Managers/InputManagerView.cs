using UnityEngine;
using UnityEngine.EventSystems;

public class InputManagerView : MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
  [SerializeField] private GunManager gunManager;

  public void OnPointerDown(PointerEventData eventData)
  {
    gunManager.UpdateAim(eventData.position);

    BaseFish hitFish = RaycastFish(eventData.position);

    if (gunManager.currentGun is LazerGun lazerGun)
    {
      if (hitFish != null) lazerGun.StartLaser(hitFish);
      else lazerGun.StopLaser();
      return;
    }

    if (gunManager.currentGun is TorpedoGun torpedoGun)
    {
      if (hitFish != null)
        torpedoGun.FireTorpedo(hitFish);
      return;
    }

    gunManager.SetBulletFiring(true);
  }


  public void OnDrag(PointerEventData eventData)
  {
    gunManager.UpdateAim(eventData.position);
  }

  public void OnPointerUp(PointerEventData eventData)
  {
    if (gunManager.currentGun is not LazerGun)
      gunManager.SetBulletFiring(false);
  }

  private BaseFish RaycastFish(Vector2 screenPos)
  {
    Ray ray = Camera.main.ScreenPointToRay(screenPos);
    RaycastHit2D hit = Physics2D.GetRayIntersection(ray);

    if (hit.collider == null)
      return null;

    return hit.collider.GetComponent<BaseFish>();
  }
}
