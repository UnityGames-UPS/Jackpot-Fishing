using UnityEngine;

public class LazerGun : BaseGun
{
  [Header("Beam")]
  [SerializeField] private RectTransform laserBeam;
  [SerializeField] private float maxLength = 2500f;
  [SerializeField] private Canvas canvas;
  [SerializeField] private Camera worldCamera;

  [Header("Impact")]
  [SerializeField] private RectTransform laserImpactAnimation;
  [SerializeField] private RectTransform laserImpactBGAnimation;
  [SerializeField] private float impactScaleFactor = 0.8f;
  [Header("Hit")]
  [SerializeField] private Color fishDamageColor = new Color(1f, 0.35f, 0.35f, 1f);
  [SerializeField] private float minDamageAnimationInterval = 0.2f;

  private BaseFish lockedFish;
  private RectTransform lockedFishRect;
  private bool laserActive;
  private bool isFiring;
  private string lockedVariant;
  private bool awaitingHitResult;
  private float nextAllowedHitTime;
  private float nextAllowedDamageAnimationTime;
  private Vector3 lastLockedPosition;
  private bool hasLastLockedPosition;
  private Vector2 impactInitSize;
  private Vector2 impactBgInitSize;
  private FishType? impactScaleType;
  private const float MinCanvasScale = 0.0001f;

  internal void Awake()
  {
    if (worldCamera == null)
      worldCamera = Camera.main;

    impactInitSize = laserImpactAnimation.sizeDelta;
    impactBgInitSize = laserImpactBGAnimation.sizeDelta;
    impactScaleType = null;

    laserBeam.gameObject.SetActive(false);
    laserImpactAnimation.gameObject.SetActive(false);
    laserImpactBGAnimation.gameObject.SetActive(false);
  }

  private void OnDisable()
  {
    StopAndClear();
  }

  private void Update()
  {
    if (!isFiring)
      return;

    if (!IsFishValidForBeam(lockedFish))
    {
      if (lockedFish != null)
      {
        lastLockedPosition = lockedFish.ColliderMidPoint;
        hasLastLockedPosition = true;
      }

      lockedFish = ResolveVariantTarget();

      if (lockedFish == null)
      {
        if (lockedVariant != null)
        {
          StopLaser();
          return;
        }

        StopAndClear();
        return;
      }
    }

    EnsureLaserActive(lockedFish);
    UpdateBeam();
    TrySendHit();
  }

  internal override void Fire()
  {
    // Laser does not use fire loop
  }

  internal void HandlePointerDown(BaseFish hitFish)
  {
    if (lockedVariant != null || lockedFish != null)
      ClearLock();

    if (!IsFishValidForBeam(hitFish))
    {
      StopAndClear();
      return;
    }

    lockedVariant = hitFish.data.variant;
    lockedFish = hitFish;
    lastLockedPosition = hitFish.ColliderMidPoint;
    hasLastLockedPosition = true;
    StartFiring();
  }

  internal void UpdateUnlockedFire(BaseFish hitFish)
  {
    if (!IsFishValidForBeam(hitFish))
    {
      StopAndClear();
      return;
    }

    lockedVariant = null;
    lockedFish = hitFish;
    lastLockedPosition = hitFish.ColliderMidPoint;
    hasLastLockedPosition = true;
    StartFiring();
  }

  internal void StopFiring()
  {
    isFiring = false;
    StopLaser();
  }

  internal void DisableTargetLock()
  {
    StopAndClear();
  }

  internal void StopLaser()
  {
    laserActive = false;

    ToggleLaserImpact(false);

    lockedFish?.StopLaserImpact();

    laserBeam.gameObject.SetActive(false);
    impactScaleType = null;
  }

  private void UpdateBeam()
  {
    if (lockedFish == null)
      return;

    Vector3 worldStart = muzzle.position;
    Vector3 worldEnd = lockedFish.GetAimPoint(worldCamera);

    // Rotate gun
    UpdateAim(worldEnd);

    Vector3 worldDir = worldEnd - worldStart;
    float worldDistance = worldDir.magnitude;

    if (worldDistance <= 0.01f)
      return;

    Vector2 screenStart = RectTransformUtility.WorldToScreenPoint(worldCamera, worldStart);
    Vector2 screenEnd = RectTransformUtility.WorldToScreenPoint(worldCamera, worldEnd);

    Vector2 dirScreen = screenEnd - screenStart;
    float screenDistance = Mathf.Min(dirScreen.magnitude, maxLength);

    if (screenDistance <= 1f)
      return;

    RectTransform beamParent = laserBeam != null ? laserBeam.parent as RectTransform : null;
    Camera uiCamera = GetCanvasCamera();
    if (beamParent != null &&
        RectTransformUtility.ScreenPointToLocalPointInRectangle(beamParent, screenStart, uiCamera, out Vector2 localStart) &&
        RectTransformUtility.ScreenPointToLocalPointInRectangle(beamParent, screenEnd, uiCamera, out Vector2 localEnd))
    {
      Vector2 localDir = localEnd - localStart;
      float canvasScale = canvas != null ? Mathf.Max(canvas.scaleFactor, MinCanvasScale) : 1f;
      float maxLengthLocal = maxLength / canvasScale;
      float localDistance = Mathf.Min(localDir.magnitude, maxLengthLocal);

      if (localDistance <= 1f)
        return;

      float angle = Mathf.Atan2(localDir.y, localDir.x) * Mathf.Rad2Deg - 90f;
      laserBeam.localRotation = Quaternion.Euler(0, 0, angle);

      Vector2 size = laserBeam.sizeDelta;
      size.y = localDistance;
      laserBeam.sizeDelta = size;

      Vector2 localMid = localStart + localDir.normalized * (localDistance * 0.5f);
      laserBeam.anchoredPosition = localMid;

      SetImpactPosition(screenEnd);
    }
    else
    {
      float angle = Mathf.Atan2(dirScreen.y, dirScreen.x) * Mathf.Rad2Deg - 90f;
      laserBeam.rotation = Quaternion.Euler(0, 0, angle);

      Vector2 size = laserBeam.sizeDelta;
      size.y = screenDistance;
      laserBeam.sizeDelta = size;

      laserImpactBGAnimation.position = worldEnd;
      laserImpactAnimation.position = worldEnd;
      laserBeam.position = worldStart + worldDir.normalized * (worldDistance * 0.5f);
    }
  }

  private void SetImpactPosition(Vector2 screenEnd)
  {
    Camera uiCamera = GetCanvasCamera();
    SetRectTransformScreenPosition(laserImpactAnimation, screenEnd, uiCamera);
    SetRectTransformScreenPosition(laserImpactBGAnimation, screenEnd, uiCamera);
  }

  private void SetRectTransformScreenPosition(RectTransform target, Vector2 screenPosition, Camera uiCamera)
  {
    if (target == null)
      return;

    RectTransform parent = target.parent as RectTransform;
    if (parent == null)
      return;

    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, uiCamera, out Vector2 localPoint))
      target.anchoredPosition = localPoint;
  }

  private Camera GetCanvasCamera()
  {
    if (canvas == null)
      return worldCamera;

    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
      return null;

    return canvas.worldCamera != null ? canvas.worldCamera : worldCamera;
  }

  private void ToggleLaserImpact(bool enable)
  {
    laserImpactBGAnimation.gameObject.SetActive(enable);
    laserImpactAnimation.gameObject.SetActive(enable);
  }

  private void UpdateLaserImpactScale()
  {
    if (!laserImpactBGAnimation.gameObject.activeInHierarchy ||
        !laserImpactAnimation.gameObject.activeInHierarchy ||
        lockedFish == null ||
        lockedFish.data == null)
      return;

    if (impactScaleType.HasValue && impactScaleType.Value == lockedFish.data.fishType)
      return;

    float typeScale = FishManager.Instance != null
      ? FishManager.Instance.GetLaserImpactScale(lockedFish.data.fishType)
      : 1f;
    float fishScale = 1f;
    if (lockedFishRect != null)
    {
      float refSize = Mathf.Max(impactBgInitSize.x, impactBgInitSize.y);
      if (refSize > 0f)
        fishScale = Mathf.Max(lockedFishRect.sizeDelta.x, lockedFishRect.sizeDelta.y) / refSize;
    }
    laserImpactBGAnimation.sizeDelta = impactBgInitSize * typeScale * fishScale;
    laserImpactAnimation.sizeDelta = impactInitSize * typeScale * fishScale * impactScaleFactor;
    impactScaleType = lockedFish.data.fishType;
    // Debug.Log($"Lazer Impact Scale updated for {lockedFish.data.fishType}: type={typeScale} fish={fishScale}");
  }

  private bool IsFishValidForBeam(BaseFish fish)
  {
    if (fish == null || fish.data == null)
      return false;

    if (fish.isDespawning || fish.PendingVisualDeath || fish.finalized)
      return false;

    if (!fish.gameObject.activeInHierarchy)
      return false;

    var fishCollider = fish.GetComponent<Collider2D>();
    if (fishCollider != null && !fishCollider.enabled)
      return false;

    return fish.IsVisibleInViewport;
  }

  private BaseFish ResolveVariantTarget()
  {
    if (lockedVariant == null)
      return null;

    BaseFish closestFish = null;
    float closestSqrDistance = float.MaxValue;

    foreach (var fish in FishManager.Instance.GetActiveFishes())
    {
      if (!IsFishValidForBeam(fish))
        continue;
      if (fish.data.variant != lockedVariant)
        continue;

      if (!hasLastLockedPosition)
        return fish;

      float sqrDistance = (fish.ColliderMidPoint - lastLockedPosition).sqrMagnitude;
      if (sqrDistance < closestSqrDistance)
      {
        closestSqrDistance = sqrDistance;
        closestFish = fish;
      }
    }

    return closestFish;
  }

  private void EnsureLaserActive(BaseFish fish)
  {
    if (lockedFish != fish || !laserActive)
    {
      lockedFish?.StopLaserImpact();

      lockedFish = fish;
      lockedFishRect = lockedFish.GetComponent<RectTransform>();
      lastLockedPosition = lockedFish.ColliderMidPoint;
      hasLastLockedPosition = true;

      lockedFish.PlayLaserImpact();

      laserActive = true;
      laserBeam.gameObject.SetActive(true);
      ToggleLaserImpact(true);
    }
    else if (lockedFishRect == null || lockedFishRect.gameObject != lockedFish.gameObject)
    {
      lockedFishRect = lockedFish.GetComponent<RectTransform>();
    }

    UpdateLaserImpactScale();
  }

  private void TrySendHit()
  {
    if (lockedFish == null)
      return;

    if (Time.time >= nextAllowedDamageAnimationTime)
    {
      StartCoroutine(lockedFish.DamageAnimation(fishDamageColor));
      nextAllowedDamageAnimationTime = Time.time + minDamageAnimationInterval;
    }

    if (awaitingHitResult)
      return;

    if (Time.time < nextAllowedHitTime)
      return;

    if (!UIManager.Instance.OnGunFired())
      return;

    awaitingHitResult = true;


    SocketIOManager.Instance.SendHitEvent(
      lockedFish.data.fishId,
      "lazer",
      lockedFish.data.variant
    );
  }

  private void StartFiring()
  {
    isFiring = true;
  }

  private void StopAndClear()
  {
    isFiring = false;
    StopLaser();
    lockedFish = null;
    lockedFishRect = null;
    lockedVariant = null;
    hasLastLockedPosition = false;
  }

  private void ClearLock()
  {
    lockedFish = null;
    lockedFishRect = null;
    lockedVariant = null;
    hasLastLockedPosition = false;
  }

  internal void OnHitResult()
  {
    awaitingHitResult = false;
    nextAllowedHitTime = Time.time + SocketIOManager.Instance.ElectricHitInterval;
  }
}
