using UnityEngine;
using DG.Tweening;

public class TorpedoGun : BaseGun
{
  [Header("Recoil")]
  [SerializeField] private float recoilDistance = 0.15f;
  [SerializeField] private float recoilDuration = 0.08f;
  [SerializeField] private float returnDuration = 0.12f;
  [SerializeField] private Ease recoilEase = Ease.OutQuad;

  private bool isFiring;
  internal bool awaitingHitResult;

  // 🔑 Variant-level lock (gameplay)
  private string lockedVariant;

  // 🔑 Instance-level lock (per shot)
  private BaseFish currentTarget;
  [SerializeField] private float minFireCooldown = 0.25f; // tweak feel
  [SerializeField] private float variantSwitchCooldown = 0.35f;
  private float nextAllowedFireTime;
  private float nextAllowedVariantSwitchTime;
  private Tween recoilTween;
  private Vector3 initialLocalPos;
  [Header("Torpedo Lock Visual")]
  [SerializeField] private RectTransform torpedoLockRect;
  [SerializeField] private float torpedoLockScaleMultiplier = 1.2f;
  [SerializeField] private float torpedoLockScaleDuration = 0.2f;
  [SerializeField] private float torpedoLockRotateDuration = 1.25f;
  private Canvas torpedoLockCanvas;
  private Vector3 torpedoLockInitScale;
  private Tween torpedoLockScaleTween;
  private Tween torpedoLockRotateTween;
  private bool torpedoLockScaleCached;
  private BaseFish torpedoLockTarget;


  // ---------------- INPUT ----------------

  internal void HandlePointerDown(BaseFish hitFish)
  {
    if (lockedVariant != null || currentTarget != null)
      ClearLock();

    if (!UIManager.Instance.IsValidTorpedoTarget(hitFish))
    {
      StopFiring();
      return;
    }

    string nextVariant = hitFish.data.variant;
    if (lockedVariant != null &&
        nextVariant != lockedVariant &&
        Time.time < nextAllowedVariantSwitchTime)
    {
      StopFiring();
      return;
    }

    lockedVariant = nextVariant;
    currentTarget = hitFish;
    nextAllowedVariantSwitchTime = Time.time + variantSwitchCooldown;
    UpdateTorpedoLockTarget(currentTarget);

    StartFiring();
  }

  internal void UpdateUnlockedFire(BaseFish hitFish)
  {
    if (!UIManager.Instance.IsValidTorpedoTarget(hitFish))
    {
      StopAndClear();
      return;
    }

    // unlocked = no variant lock
    lockedVariant = null;
    currentTarget = hitFish;
    ClearTorpedoLockVisual();

    StartFiring();
  }

  internal void StopFiring()
  {
    isFiring = false;
  }

  internal void DisableTargetLock()
  {
    StopAndClear();
  }

  // ---------------- LOOP ----------------

  private void Update()
  {
    MaintainLockTarget();

    if (!isFiring)
      return;

    if (awaitingHitResult)
      return;

    // 🆕 local cooldown (prevents machine gun)
    if (Time.time < nextAllowedFireTime)
      return;

    if (!IsFishValidForShot(currentTarget))
    {
      if (lockedVariant != null)
        return;

      StopAndClear();
      return;
    }

    FireAtCurrentTarget();
  }


  internal void StartFiring()
  {
    isFiring = true;
  }

  // ---------------- FIRE ----------------

  private void FireAtCurrentTarget()
  {
    if (!UIManager.Instance.OnGunFired())
      return;

    PlayRecoil();

    BaseFish fish = currentTarget;

    awaitingHitResult = true;

    TorpedoBulletView torpedo =
      TorpedoPool.Instance.GetFromPool();

    torpedo.transform.SetPositionAndRotation(
      muzzle.position,
      Quaternion.identity
    );

    torpedo.Init(fish);

    SocketIOManager.Instance.SendHitEvent(
      fish.data.fishId,
      "torpedo",
      fish.data.variant
    );

    // 🆕 prevent rapid refire
    nextAllowedFireTime = Time.time + minFireCooldown;
  }

  private void PlayRecoil()
  {
    recoilTween?.Kill();

    Vector3 recoilDir = -transform.up; // opposite of firing direction

    recoilTween = transform
      .DOLocalMove(initialLocalPos + recoilDir * recoilDistance, recoilDuration)
      .SetEase(recoilEase)
      .OnComplete(() =>
      {
        transform
          .DOLocalMove(initialLocalPos, returnDuration)
          .SetEase(Ease.OutQuad);
      });
  }


  // ---------------- TARGET RESOLUTION ----------------

  private BaseFish ResolveVariantTarget()
  {
    if (lockedVariant == null)
      return null;

    foreach (var fish in FishManager.Instance.GetActiveFishes())
    {
      if (IsFishValidForShot(fish) &&
          fish.data.variant == lockedVariant)
      {
        return fish;
      }
    }

    return null;
  }

  private bool IsFishValidForShot(BaseFish fish)
  {
    if (fish == null)
      return false;

    if (fish.isDespawning || fish.PendingVisualDeath)
      return false;

    if (!fish.gameObject.activeInHierarchy)
      return false;

    return fish.TorpedoTargetVisible;
  }

  // ---------------- BACKEND CALLBACK ----------------

  internal void OnFishKilled(BaseFish fish)
  {
    if (currentTarget == fish)
    {
      currentTarget = null;
      if (Time.time < nextAllowedVariantSwitchTime)
      {
        if (torpedoLockTarget == fish)
          ClearTorpedoLockVisual();
        return;
      }

      BaseFish resolved = ResolveVariantTarget();
      if (resolved != null)
      {
        currentTarget = resolved;
        nextAllowedVariantSwitchTime = Time.time + variantSwitchCooldown;
        UpdateTorpedoLockTarget(currentTarget);
        return;
      }

      if (torpedoLockTarget == fish)
        ClearTorpedoLockVisual();
    }
  }

  internal BaseFish GetLockedFish()
  {
    return currentTarget;
  }

  private void StopAndClear()
  {
    isFiring = false;
    awaitingHitResult = false;
    currentTarget = null;
    lockedVariant = null;
    ClearTorpedoLockVisual();
  }

  private void ClearLock()
  {
    currentTarget = null;
    lockedVariant = null;
    ClearTorpedoLockVisual();
  }

  internal void Awake()
  {
    initialLocalPos = transform.localPosition;
    if (torpedoLockRect != null)
    {
      torpedoLockCanvas = torpedoLockRect.GetComponentInParent<Canvas>();
      torpedoLockRect.gameObject.SetActive(false);
    }
  }

  internal override void Fire() { }

  private void MaintainLockTarget()
  {
    if (lockedVariant == null)
    {
      ClearTorpedoLockVisual();
      return;
    }

    if (IsFishValidForShot(currentTarget))
    {
      UpdateTorpedoLockTarget(currentTarget);
      return;
    }

    UpdateTorpedoLockTarget(null);

    if (Time.time < nextAllowedVariantSwitchTime)
      return;

    BaseFish resolved = ResolveVariantTarget();
    if (resolved == null)
      return;

    if (resolved != currentTarget)
      nextAllowedVariantSwitchTime = Time.time + variantSwitchCooldown;

    currentTarget = resolved;
    UpdateTorpedoLockTarget(currentTarget);
  }

  private void UpdateTorpedoLockTarget(BaseFish target)
  {
    if (torpedoLockRect == null)
      return;

    if (torpedoLockTarget == target)
    {
      if (torpedoLockTarget != null)
        UpdateTorpedoLockPosition(torpedoLockTarget.ColliderMidPoint);
      return;
    }

    torpedoLockTarget = target;

    if (torpedoLockTarget == null)
    {
      ClearTorpedoLockVisual();
      return;
    }

    StartTorpedoLockVisual();
    UpdateTorpedoLockPosition(torpedoLockTarget.ColliderMidPoint);
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

  private void UpdateTorpedoLockPosition(Vector3 worldPos)
  {
    if (torpedoLockRect == null)
      return;

    Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
    RectTransform canvasRect = torpedoLockCanvas != null
      ? torpedoLockCanvas.transform as RectTransform
      : null;
    Camera uiCamera = null;

    if (torpedoLockCanvas != null &&
        torpedoLockCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
      uiCamera = torpedoLockCanvas.worldCamera;

    if (canvasRect != null &&
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
          canvasRect, screenPos, uiCamera, out Vector3 uiPos))
    {
      torpedoLockRect.position = uiPos;
      return;
    }

    torpedoLockRect.position = screenPos;
  }
}
