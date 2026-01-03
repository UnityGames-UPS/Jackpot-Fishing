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
  [SerializeField] private float impactScaleFactor = 0.8f;

  private BaseFish lockedFish;
  private RectTransform lockedFishRect;
  private bool laserActive;

  internal override void Awake()
  {
    base.Awake();
    if (worldCamera == null)
      worldCamera = Camera.main;

    laserBeam.gameObject.SetActive(false);
    laserImpactAnimation.gameObject.SetActive(false);
  }

  private void Update()
  {
    if (!laserActive || lockedFish == null)
      return;

    if (!IsFishVisible(lockedFish))
    {
      StopLaser();
      return;
    }

    UpdateBeam();
  }

  internal override void Fire()
  {
    // Laser does not use fire loop
  }

  internal void StartLaser(BaseFish fish)
  {
    if (fish == null)
      return;

    if (lockedFish != fish)
    {
      lockedFish?.StopLaserImpact();

      lockedFish = fish;
      lockedFishRect = lockedFish.GetComponent<RectTransform>();

      lockedFish.PlayLaserImpact();
    }

    laserActive = true;
    laserBeam.gameObject.SetActive(true);

    ToggleLaserImpact(true);
    UpdateBeam();
  }

  internal void StopLaser()
  {
    laserActive = false;

    ToggleLaserImpact(false);

    lockedFish?.StopLaserImpact();
    lockedFish = null;
    lockedFishRect = null;

    laserBeam.gameObject.SetActive(false);
  }

  private void UpdateBeam()
  {
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

    laserImpactAnimation.position = lockedFish.HitPoint.position;
    laserBeam.position = worldStart + worldDir.normalized * (worldDistance * 0.5f);
  }

  private void ToggleLaserImpact(bool enable)
  {
    if (laserImpactAnimation == null || lockedFishRect == null)
      return;

    if (enable)
    {
      // Scale impact relative to fish size
      laserImpactAnimation.sizeDelta = lockedFishRect.sizeDelta * impactScaleFactor;
    }

    laserImpactAnimation.gameObject.SetActive(enable);
  }

  private const float margin = 0.05f;
  private bool IsFishVisible(BaseFish fish)
  {
    Vector3 viewportPos = worldCamera.WorldToViewportPoint(fish.transform.position);
    return viewportPos.z > 0 &&
           viewportPos.x > -margin && viewportPos.x < 1 + margin &&
           viewportPos.y > -margin && viewportPos.y < 1 + margin;
  }
}
