using System;
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

  [Header("Firing")]
  [Tooltip("Seconds between laser ticks, used only until init sends the weapon's fireIntervalMs.")]
  [SerializeField] private float fallbackHitInterval = 0.2f;
  [Tooltip("SHOOTs still waiting for an ack before the beam stops sending more.")]
  [SerializeField] private int maxPendingShots = 3;

  // An opponent's beam goes away if no shot_result keeps it alive for this long.
  private const float RemoteBeamTimeout = 0.5f;

  private BaseFish lockedFish;
  private RectTransform lockedFishRect;
  private bool laserActive;
  private bool isFiring;
  private string lockedVariant;
  private int pendingShots;
  private bool remote; // drawing an opponent's beam
  private float remoteBeamUntil;
  private string streamFishId; // fish of our last laser SHOOT; LASER_STOP is owed while set
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

    // The beam mask grows from the muzzle: its bottom edge stays on the gun mouth and only its
    // height changes, so the fixed-size laser strip inside is revealed, never stretched.
    laserBeam.pivot = new Vector2(0.5f, 0f);

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

    if (remote && Time.time > remoteBeamUntil)
    {
      StopAndClear();
      return;
    }

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
    EndStream();
    laserActive = false;

    ToggleLaserImpact(false);

    if (lockedFish != null)
      lockedFish.StopLaserImpact();

    // Also runs from OnDisable while exiting Play mode, when the beam may already be destroyed.
    if (laserBeam != null)
      laserBeam.gameObject.SetActive(false);
    impactScaleType = null;
  }

  private void UpdateBeam()
  {
    if (lockedFish == null)
      return;

    Vector3 worldStart = muzzle.position;
    Vector3 worldEnd = lockedFish.GetAimPoint();

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

      laserBeam.anchoredPosition = localStart;

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
      laserBeam.position = worldStart;
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
    if (laserImpactBGAnimation != null)
      laserImpactBGAnimation.gameObject.SetActive(enable);
    if (laserImpactAnimation != null)
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

    // Not GetComponent<Collider2D>(): that returns the prefab's disabled BoxCollider2D (hit circles
    // are added at runtime), so every fish failed. Kill/Leave leave Alive and turn picking off together.
    if (fish.State != FishState.Alive)
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

    if (remote || Time.time < nextAllowedHitTime || pendingShots >= maxPendingShots)
      return;

    var socket = SocketIOManager.Instance;
    if (socket == null || !socket.CanShoot || !Playfield.IsReady)
      return;

    double now = ServerClock.NowMs;
    if (!lockedFish.IsHittable(now))
      return;

    if (!UIManager.Instance.OnGunFired())
    {
      StopAndClear();
      return;
    }

    // The laser sends no FIRE: the server charges each SHOOT directly (a FIRE would charge twice).
    string bulletId = "laser_" + Guid.NewGuid().ToString("N").Substring(0, 12);
    string fishId = lockedFish.Id;
    int betIndex = UIManager.Instance.BetCounter;
    Vector2 origin = socket.SeatOrigin;
    Vector2 aimTarget = lockedFish.AimServerPointAt(now);

    pendingShots++;
    streamFishId = fishId;
    nextAllowedHitTime = Time.time + socket.FireIntervalFor(MLFGWeaponIds.Laser, fallbackHitInterval);

    if (GameLog.IsOn(LogCategory.Bullet))
      GameLog.Info(LogCategory.Bullet, $"Laser {bulletId} -> {fishId} ({lockedFish.data?.variant}) at ({aimTarget.x:F0},{aimTarget.y:F0})");

    socket.SendShoot(MLFGWeaponIds.Laser, bulletId, origin, origin, aimTarget, 0, betIndex, fishId, now,
      _ => pendingShots = Mathf.Max(0, pendingShots - 1));
  }

  // Tells the server the beam stopped, once per stop. Moving to a new target is not a stop:
  // the next SHOOT just carries the new targetFishId.
  private void EndStream()
  {
    if (streamFishId == null)
      return;

    string fishId = streamFishId;
    streamFishId = null;
    // Also runs from OnDisable while exiting Play mode.
    if (SocketIOManager.Instance != null)
      SocketIOManager.Instance.SendLaserStop(fishId);
  }

  private void StartFiring()
  {
    isFiring = true;
  }

  // Opponent's laser tick (their shot_result): beam only, never sends anything. Each tick keeps
  // the beam up for RemoteBeamTimeout, so steady ticks look continuous and it goes away once they stop.
  internal void PlayRemoteTick(BaseFish fish)
  {
    if (!IsFishValidForBeam(fish))
      return;

    remote = true;
    lockedVariant = null;
    EnsureLaserActive(fish);
    remoteBeamUntil = Time.time + RemoteBeamTimeout;
    StartFiring();
  }

  // game:laser_stopped from the opponent on this rig.
  internal void StopRemote()
  {
    if (remote)
      StopAndClear();
  }

  private void StopAndClear()
  {
    isFiring = false;
    remote = false;
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

  // Old-backend hit result (LegacyHitResultHandler). ML-FG shots are paced by fireIntervalMs.
  internal void OnHitResult() { }
}
