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
  [SerializeField] private float viewportPadding = 0.1f;

  private BaseFish lockedFish;
  private RectTransform lockedFishRect;
  private bool laserActive;
  private bool isFiring;
  private string lockedVariant;
  private bool awaitingHitResult;
  private float nextAllowedHitTime;

  internal void Awake()
  {
    if (worldCamera == null)
      worldCamera = Camera.main;

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
    StartFiring();
  }

  internal void StopFiring()
  {
    isFiring = false;
    StopLaser();
  }

  internal void StopLaser()
  {
    laserActive = false;

    ToggleLaserImpact(false);

    lockedFish?.StopLaserImpact();

    laserBeam.gameObject.SetActive(false);
  }

  private void UpdateBeam()
  {
    if (lockedFish == null)
      return;

    Vector3 worldStart = muzzle.position;
    Vector3 worldEnd = lockedFish.HitPoint.position;

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

    float angle = Mathf.Atan2(dirScreen.y, dirScreen.x) * Mathf.Rad2Deg - 90f;
    laserBeam.rotation = Quaternion.Euler(0, 0, angle);

    Vector2 size = laserBeam.sizeDelta;
    size.y = screenDistance;
    laserBeam.sizeDelta = size;

    laserImpactBGAnimation.position = lockedFish.HitPoint.position;
    laserImpactAnimation.position = lockedFish.HitPoint.position;
    laserBeam.position = worldStart + worldDir.normalized * (worldDistance * 0.5f);
  }

  private void ToggleLaserImpact(bool enable)
  {
    if (enable && lockedFishRect)
    {
      // Scale impact relative to fish size
      laserImpactBGAnimation.sizeDelta = lockedFishRect.sizeDelta * lockedFish.data.laserImpactScaleFactor;
      laserImpactAnimation.sizeDelta = lockedFishRect.sizeDelta * impactScaleFactor;
    }

    laserImpactBGAnimation.gameObject.SetActive(enable);
    laserImpactAnimation.gameObject.SetActive(enable);
  }

  private const float margin = 0.05f;
  private bool IsFishVisible(BaseFish fish)
  {
    var cam = Camera.main;
    var bounds = fish.GetComponent<BoxCollider2D>().bounds;

    Vector3 min = cam.WorldToViewportPoint(bounds.min);
    Vector3 max = cam.WorldToViewportPoint(bounds.max);

    if (max.z <= 0)
      return false;

    return max.x > viewportPadding &&
           min.x < 1f - viewportPadding &&
           max.y > viewportPadding &&
           min.y < 1f - viewportPadding;
  }

  private bool IsFishValidForBeam(BaseFish fish)
  {
    if (fish == null || fish.data == null)
      return false;

    if (fish.isDespawning || fish.PendingVisualDeath)
      return false;

    if (!fish.gameObject.activeInHierarchy)
      return false;

    return IsFishVisible(fish);
  }

  private BaseFish ResolveVariantTarget()
  {
    if (lockedVariant == null)
      return null;

    foreach (var fish in FishManager.Instance.GetActiveFishes())
    {
      if (IsFishValidForBeam(fish) &&
          fish.data.variant == lockedVariant)
      {
        return fish;
      }
    }

    return null;
  }

  private void EnsureLaserActive(BaseFish fish)
  {
    if (lockedFish != fish || !laserActive)
    {
      lockedFish?.StopLaserImpact();

      lockedFish = fish;
      lockedFishRect = lockedFish.GetComponent<RectTransform>();

      lockedFish.PlayLaserImpact();

      laserActive = true;
      laserBeam.gameObject.SetActive(true);
      ToggleLaserImpact(true);
    }
  }

  private void TrySendHit()
  {
    if (lockedFish == null)
      return;

    if (awaitingHitResult)
      return;

    if (Time.time < nextAllowedHitTime)
      return;

    if (!UIManager.Instance.OnGunFired())
      return;

    awaitingHitResult = true;

    StartCoroutine(lockedFish.DamageAnimation(fishDamageColor));

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
  }

  private void ClearLock()
  {
    lockedFish = null;
    lockedFishRect = null;
    lockedVariant = null;
  }

  internal void OnHitResult()
  {
    awaitingHitResult = false;
    nextAllowedHitTime = Time.time + SocketIOManager.Instance.ElectricHitInterval;
  }
}
