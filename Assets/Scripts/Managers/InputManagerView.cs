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
  [SerializeField] private RectTransform crosshairRect;
  [SerializeField] private Canvas crosshairCanvas;

  void Awake()
  {
    Instance = this;
    if (crosshairRect != null && crosshairCanvas == null)
      crosshairCanvas = crosshairRect.GetComponentInParent<Canvas>();
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
    UpdatePointer(eventData.position);

    BaseFish hitFish = RaycastFish(lastPointerScreenPos);

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
    UpdatePointer(eventData.position);

    if (GunManager.Instance.currentGun is LazerGun lazerGun &&
        !UIManager.Instance.IsTargetLockEnabled)
    {
      BaseFish hitFish = RaycastFish(lastPointerScreenPos);
      lazerGun.UpdateUnlockedFire(hitFish);
      currentPointerFish = hitFish;
      return;
    }

    if (GunManager.Instance.currentGun is TorpedoGun torpedoGun &&
        !UIManager.Instance.IsTargetLockEnabled)
    {
      BaseFish hitFish = RaycastFish(lastPointerScreenPos);
      torpedoGun.UpdateUnlockedFire(hitFish);
      currentPointerFish = hitFish;
    }
  }


  public void OnPointerUp(PointerEventData eventData)
  {
    pointerHeld = false;
    currentPointerFish = null;
    SetCrosshairActive(false);

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
      return RaycastFishAtPoint(screenPos);

    BaseFish fish = hit.collider.GetComponent<BaseFish>();
    return fish != null ? fish : RaycastFishAtPoint(screenPos);
  }

  private BaseFish RaycastFishAtPoint(Vector2 screenPos)
  {
    Camera cam = Camera.main;
    if (cam == null)
      return null;

    float z = Mathf.Abs(cam.transform.position.z);
    Vector3 worldPoint = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));
    Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
    for (int i = 0; i < hits.Length; i++)
    {
      BaseFish fish = hits[i].GetComponent<BaseFish>();
      if (fish != null)
        return fish;
    }

    return null;
  }

  internal BaseFish GetCurrentPointerFish()
  {
    return pointerHeld ? currentPointerFish : null;
  }

  private void UpdatePointer(Vector2 screenPos)
  {
    lastPointerScreenPos = AdjustScreenPos(screenPos);
    GunManager.Instance.UpdateAim(lastPointerScreenPos);
    SetCrosshairActive(true);
    UpdateCrosshairPosition(lastPointerScreenPos);
  }

  private Vector2 AdjustScreenPos(Vector2 screenPos)
  {
    if (OrientationChange.Instance == null || OrientationChange.Instance.IsLandscape)
      return screenPos;

    Camera mainCamera = Camera.main;
    if (mainCamera == null)
      return screenPos;

    Vector3 viewportPos = mainCamera.ScreenToViewportPoint(screenPos);

    // Keep axes aligned with touch; just map into the camera's pixel rect.
    float rotatedX = viewportPos.x;
    float rotatedY = viewportPos.y;

    return new Vector2(
      rotatedX * mainCamera.pixelWidth,
      rotatedY * mainCamera.pixelHeight
    );
  }

  private void SetCrosshairActive(bool active)
  {
    if (crosshairRect == null)
      return;

    if (crosshairRect.gameObject.activeSelf != active)
      crosshairRect.gameObject.SetActive(active);
  }

  private void UpdateCrosshairPosition(Vector2 screenPos)
  {
    if (crosshairRect == null)
      return;

    RectTransform canvasRect = crosshairCanvas != null
      ? crosshairCanvas.transform as RectTransform
      : null;
    Camera uiCamera = null;

    if (crosshairCanvas != null &&
        crosshairCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
      uiCamera = crosshairCanvas.worldCamera;

    if (canvasRect != null &&
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
          canvasRect, screenPos, uiCamera, out Vector3 worldPos))
    {
      crosshairRect.position = worldPos;
      return;
    }

    crosshairRect.position = screenPos;
  }

}
