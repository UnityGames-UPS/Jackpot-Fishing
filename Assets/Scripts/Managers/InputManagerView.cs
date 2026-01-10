using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

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
  [Header("Torpedo Lock Visual")]
  [SerializeField] private RectTransform torpedoLockRect;
  [SerializeField] private float torpedoLockScaleMultiplier = 1.2f;
  [SerializeField] private float torpedoLockScaleDuration = 0.2f;
  [SerializeField] private float torpedoLockRotateDuration = 1.25f;
  private Vector3 torpedoLockInitScale;
  private Tween torpedoLockScaleTween;
  private Tween torpedoLockRotateTween;
  private bool torpedoLockScaleCached;
  private BaseFish torpedoLockTarget;

  void Awake()
  {
    Instance = this;
    if (crosshairRect != null && crosshairCanvas == null)
      crosshairCanvas = crosshairRect.GetComponentInParent<Canvas>();
    if (torpedoLockRect != null)
      torpedoLockRect.gameObject.SetActive(false);
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

    UpdateTorpedoLockVisual();

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
    SetCrosshairActive(true);
    UpdateCrosshairPosition(eventData.position);

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
    SetCrosshairActive(true);
    UpdateCrosshairPosition(eventData.position);

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
      return null;

    return hit.collider.GetComponent<BaseFish>();
  }

  internal BaseFish GetCurrentPointerFish()
  {
    return pointerHeld ? currentPointerFish : null;
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

  private void UpdateTorpedoLockVisual()
  {
    if (torpedoLockRect == null)
      return;

    if (!UIManager.Instance.IsTargetLockEnabled ||
        !(GunManager.Instance.currentGun is TorpedoGun torpedoGun))
    {
      ClearTorpedoLockVisual();
      return;
    }

    BaseFish locked = torpedoGun.GetLockedFish();
    if (locked == null)
    {
      ClearTorpedoLockVisual();
      return;
    }

    if (torpedoLockTarget != locked)
    {
      ClearTorpedoLockVisual();
      torpedoLockTarget = locked;
      StartTorpedoLockVisual();
    }

    Vector3 screenPos = Camera.main.WorldToScreenPoint(locked.transform.position);
    UpdateTorpedoLockPosition(screenPos);
  }

  private void StartTorpedoLockVisual()
  {
    if (torpedoLockRect == null)
      return;

    if (!torpedoLockScaleCached)
    {
      torpedoLockInitScale = torpedoLockRect.localScale;
      torpedoLockScaleCached = true;
    }

    torpedoLockRect.gameObject.SetActive(true);
    torpedoLockRect.localRotation = Quaternion.identity;
    torpedoLockRect.localScale = torpedoLockInitScale * torpedoLockScaleMultiplier;

    torpedoLockScaleTween?.Kill();
    torpedoLockRotateTween?.Kill();

    torpedoLockScaleTween = torpedoLockRect
      .DOScale(torpedoLockInitScale, torpedoLockScaleDuration)
      .SetEase(Ease.OutQuad);

    torpedoLockRotateTween = torpedoLockRect
      .DORotate(new Vector3(0f, 0f, 360f), torpedoLockRotateDuration, RotateMode.FastBeyond360)
      .SetLoops(-1, LoopType.Restart)
      .SetEase(Ease.Linear);
  }

  private void ClearTorpedoLockVisual()
  {
    if (torpedoLockRect == null)
      return;

    torpedoLockScaleTween?.Kill();
    torpedoLockRotateTween?.Kill();
    torpedoLockRect.localRotation = Quaternion.identity;
    if (torpedoLockScaleCached)
      torpedoLockRect.localScale = torpedoLockInitScale;
    if (torpedoLockRect.gameObject.activeSelf)
      torpedoLockRect.gameObject.SetActive(false);
    torpedoLockTarget = null;
  }

  private void UpdateTorpedoLockPosition(Vector2 screenPos)
  {
    if (torpedoLockRect == null)
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
      torpedoLockRect.position = worldPos;
      return;
    }

    torpedoLockRect.position = screenPos;
  }
}
