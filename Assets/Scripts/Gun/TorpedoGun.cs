using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class TorpedoGun : BaseGun
{
  [Header("Recoil")]
  [SerializeField] private float recoilDistance = 0.15f;
  [SerializeField] private float recoilDuration = 0.08f;
  [SerializeField] private float returnDuration = 0.12f;
  [SerializeField] private Ease recoilEase = Ease.OutQuad;

  [Header("Muzzle Flash")]
  [SerializeField] private float muzzleFadeIn = 0.03f;
  [SerializeField] private float muzzleFadeOut = 0.08f;
  [SerializeField] private float muzzleFadeInterval = 0.05f;

  private bool isFiring;

  // 🔑 Variant-level lock (gameplay)
  private string lockedVariant;

  // 🔑 Instance-level lock (per shot)
  private BaseFish currentTarget;
  [Header("Torpedo Fire Intervals")]
  [Tooltip("Seconds between torpedoes, used only until init sends the weapon's fireIntervalMs.")]
  [SerializeField] private float minFireCooldown = 0.25f;
  [SerializeField] private float variantSwitchCooldown = 0.35f;
  private float nextAllowedFireTime;
  private float nextAllowedVariantSwitchTime;
  private Tween recoilTween;
  private Tween muzzleTween;
  private Image muzzleImage;
  private Vector3 initialLocalPos;
  [Header("Torpedo Lock Visual")]
  [SerializeField] private RectTransform torpedoLockRect;
  [SerializeField] private float torpedoLockScaleMultiplier = 1.2f;
  [SerializeField] private float torpedoLockScaleDuration = 0.2f;
  [SerializeField] private float torpedoLockRotateDuration = 1.25f;
  [Header("Torpedo Spawn Point")]
  [SerializeField] private Transform torpedoSpawnPoint;
  private Canvas torpedoLockCanvas;
  private Vector3 torpedoLockInitScale;
  private Tween torpedoLockScaleTween;
  private Tween torpedoLockRotateTween;
  private bool torpedoLockScaleCached;
  private BaseFish torpedoLockTarget;
  private Vector3 lastLockedPosition;
  private bool hasLastLockedPosition;


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
    lastLockedPosition = hitFish.ColliderMidPoint;
    hasLastLockedPosition = true;
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
    lastLockedPosition = hitFish.ColliderMidPoint;
    hasLastLockedPosition = true;
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
    if (!IsFishValidForShot(currentTarget))
    {
      BaseFish invalidTarget = currentTarget;
      currentTarget = null;
      if (torpedoLockTarget == invalidTarget)
        ClearTorpedoLockVisual();
      if (lockedVariant != null)
        return;
      StopAndClear();
      return;
    }

    var socket = SocketIOManager.Instance;
    if (socket == null || !socket.CanShoot || !Playfield.IsReady)
      return;

    if (!UIManager.Instance.OnGunFired())
      return;

    PlayMuzzleFlash();
    PlayRecoil();

    BaseFish fish = currentTarget;
    string bulletId = "torpedo_" + Guid.NewGuid().ToString("N").Substring(0, 12);
    int betIndex = UIManager.Instance.BetCounter;
    Vector2 origin = socket.SeatOrigin;
    Vector2 direction = (fish.ServerPositionAt(ServerClock.NowMs) - origin).normalized;
    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    var shot = new TorpedoShot(bulletId, fish.Id, origin, betIndex);

    TorpedoBulletView torpedo =
      TorpedoPool.Instance.GetFromPool();

    torpedo.transform.SetPositionAndRotation(
      torpedoSpawnPoint.position,
      Quaternion.identity
    );

    torpedo.Init(fish, shot);

    if (GameLog.IsOn(LogCategory.Bullet))
      GameLog.Info(LogCategory.Bullet, $"Torpedo {bulletId} -> {fish.Id} ({fish.data?.variant}) angle {angle:F1}");
    socket.SendFire(MLFGWeaponIds.Torpedo, bulletId, origin, angle, betIndex, shot.OnFireAccepted, shot.OnFireRejected);

    // The server rejects a FIRE sooner than fireIntervalMs.
    nextAllowedFireTime = Time.time + socket.FireIntervalFor(MLFGWeaponIds.Torpedo, minFireCooldown);
  }

  // Opponent's torpedo (bullet_fired): visual only, never sends FIRE or SHOOT. With no live target
  // (already dead or off screen) it just turns and flashes.
  internal void PlayRemoteShot(BaseFish fish, float serverAngle)
  {
    bool hasTarget = IsFishValidForShot(fish);
    if (hasTarget)
    {
      RotateGun(fish.GetAimPoint(Camera.main));
    }
    else if (Playfield.IsReady)
    {
      float rad = serverAngle * Mathf.Deg2Rad;
      RotateGun(transform.position + Playfield.ServerDirToWorld(new Vector2(Mathf.Cos(rad), Mathf.Sin(rad))));
    }

    PlayMuzzleFlash();
    PlayRecoil();
    if (!hasTarget)
      return;

    TorpedoBulletView torpedo = TorpedoPool.Instance.GetFromPool();
    torpedo.transform.SetPositionAndRotation(torpedoSpawnPoint.position, Quaternion.identity);
    torpedo.Init(fish, null); // no TorpedoShot, so no SHOOT on impact
  }

  private void PlayRecoil()
  {
    recoilTween?.Kill();

    Vector3 recoilDir = transform.localRotation * Vector3.down; // opposite of firing direction, in parent space

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

  private void PlayMuzzleFlash()
  {
    if (muzzleImage == null)
      return;

    muzzleTween?.Kill();

    muzzleTween = DOTween.Sequence()
      .Append(muzzleImage.DOFade(1f, muzzleFadeIn))
      .AppendInterval(muzzleFadeInterval)
      .Append(muzzleImage.DOFade(0f, muzzleFadeOut));
  }


  // ---------------- TARGET RESOLUTION ----------------

  private BaseFish ResolveVariantTarget()
  {
    if (lockedVariant == null)
      return null;

    BaseFish closestFish = null;
    float closestSqrDistance = float.MaxValue;

    foreach (var fish in FishManager.Instance.GetActiveFishes())
    {
      if (!IsFishValidForShot(fish))
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

  private bool IsFishValidForShot(BaseFish fish)
  {
    if (fish == null || fish.data == null)
      return false;

    if (fish.isDespawning || fish.PendingVisualDeath || fish.finalized)
      return false;

    if (!fish.gameObject.activeInHierarchy)
      return false;

    if (fish is EffectFish effectFish && !effectFish.IsTorpedoTargetable)
      return false;

    return fish.IsVisibleInViewport;
  }

  // ---------------- BACKEND CALLBACK ----------------

  internal void OnFishKilled(BaseFish fish)
  {
    if (currentTarget == fish)
    {
      lastLockedPosition = fish.ColliderMidPoint;
      hasLastLockedPosition = true;
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
    currentTarget = null;
    lockedVariant = null;
    hasLastLockedPosition = false;
    ClearTorpedoLockVisual();
  }

  private void ClearLock()
  {
    currentTarget = null;
    lockedVariant = null;
    hasLastLockedPosition = false;
    ClearTorpedoLockVisual();
  }

  internal void Awake()
  {
    initialLocalPos = transform.localPosition;
    muzzleImage = muzzle.GetComponent<Image>();
    if (muzzleImage != null)
      muzzleImage.color = new Color(1f, 1f, 1f, 0f);
    if (torpedoLockRect != null)
    {
      torpedoLockCanvas = torpedoLockRect.GetComponentInParent<Canvas>();
      torpedoLockRect.gameObject.SetActive(false);
    }
  }

  private void OnDisable()
  {
    StopAndClear();
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
      lastLockedPosition = currentTarget.ColliderMidPoint;
      hasLastLockedPosition = true;
      UpdateTorpedoLockTarget(currentTarget);
      return;
    }

    if (currentTarget != null)
    {
      lastLockedPosition = currentTarget.ColliderMidPoint;
      hasLastLockedPosition = true;
    }
    currentTarget = null;
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
        UpdateTorpedoLockPosition(torpedoLockTarget.GetAimPoint(Camera.main));
      return;
    }

    torpedoLockTarget = target;

    if (torpedoLockTarget == null)
    {
      ClearTorpedoLockVisual();
      return;
    }

    StartTorpedoLockVisual();
    UpdateTorpedoLockPosition(torpedoLockTarget.GetAimPoint(Camera.main));
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
