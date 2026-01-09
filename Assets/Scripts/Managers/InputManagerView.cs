using UnityEngine;
using UnityEngine.EventSystems;

public class InputManagerView : MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
  public static InputManagerView Instance;
  private BaseFish currentPointerFish;
  private Vector2 lastPointerScreenPos;
  private bool pointerHeld;

  void Awake()
  {
    Instance = this;
  }

  void Update()
  {
    if (UIManager.Instance.IsTargetLockEnabled)
    {
      BaseFish locked =
        (GunManager.Instance.currentGun as TorpedoGun)?.GetLockedFish();

      if (locked != null)
      {
        Vector3 screenPos =
          Camera.main.WorldToScreenPoint(locked.transform.position);

        GunManager.Instance.UpdateAim(screenPos);
      }
    }

    if (!pointerHeld)
      return;

    // Keep sampling fish even when pointer does NOT move
    currentPointerFish = RaycastFish(lastPointerScreenPos);

    if (GunManager.Instance.currentGun is TorpedoGun torpedoGun &&
        !UIManager.Instance.IsTargetLockEnabled)
      torpedoGun.UpdateUnlockedFire(currentPointerFish);
  }

  public void OnPointerDown(PointerEventData eventData)
  {
    pointerHeld = true;
    lastPointerScreenPos = eventData.position;
    GunManager.Instance.UpdateAim(eventData.position);

    BaseFish hitFish = RaycastFish(eventData.position);

    if (GunManager.Instance.currentGun is LazerGun lazerGun)
    {
      if (UIManager.Instance.IsTargetLockEnabled)
        lazerGun.HandlePointerDown(hitFish);
      else
      {
        lazerGun.UpdateUnlockedFire(hitFish);
        currentPointerFish = hitFish;
      }

      return;
    }

    if (GunManager.Instance.currentGun is TorpedoGun torpedoGun)
    {
      if (UIManager.Instance.IsTargetLockEnabled)
      {
        torpedoGun.HandlePointerDown(hitFish);
      }
      else
      {
        torpedoGun.UpdateUnlockedFire(hitFish);
        currentPointerFish = hitFish;
      }

      return;
    }

    GunManager.Instance.SetBulletFiring(true);
  }


  public void OnDrag(PointerEventData eventData)
  {
    pointerHeld = true;
    lastPointerScreenPos = eventData.position;
    GunManager.Instance.UpdateAim(eventData.position);

    if (GunManager.Instance.currentGun is LazerGun lazerGun &&
        !UIManager.Instance.IsTargetLockEnabled)
    {
      BaseFish hitFish = RaycastFish(eventData.position);
      lazerGun.UpdateUnlockedFire(hitFish);
      currentPointerFish = hitFish;
      return;
    }

    if (GunManager.Instance.currentGun is TorpedoGun torpedoGun &&
        !UIManager.Instance.IsTargetLockEnabled)
    {
      BaseFish hitFish = RaycastFish(eventData.position);
      torpedoGun.UpdateUnlockedFire(hitFish);
      currentPointerFish = hitFish;
    }
  }


  public void OnPointerUp(PointerEventData eventData)
  {
    pointerHeld = false;
    currentPointerFish = null;
    currentPointerFish = null;

    if (GunManager.Instance.currentGun is LazerGun lazerGun)
    {
      if (!UIManager.Instance.IsTargetLockEnabled)
        lazerGun.StopFiring();
      return;
    }

    if (GunManager.Instance.currentGun is TorpedoGun torpedoGun)
    {
      if (!UIManager.Instance.IsTargetLockEnabled)
        torpedoGun.StopFiring();
      return;
    }

    if (GunManager.Instance.currentGun is SimpleGun gun)
      GunManager.Instance.SetBulletFiring(false);

  }

  private BaseFish RaycastFish(Vector2 screenPos)
  {
    Ray ray = Camera.main.ScreenPointToRay(screenPos);
    RaycastHit2D hit = Physics2D.GetRayIntersection(ray);

    if (hit.collider == null)
      return null;

    return hit.collider.GetComponent<BaseFish>();
  }

  internal BaseFish GetCurrentPointerFish()
  {
    return pointerHeld ? currentPointerFish : null;
  }

}
