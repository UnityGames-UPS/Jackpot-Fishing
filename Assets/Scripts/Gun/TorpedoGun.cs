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
  [SerializeField] private float viewportPadding = 0.1f;
  private float nextAllowedFireTime;
  private float nextAllowedVariantSwitchTime;
  private Tween recoilTween;
  private Vector3 initialLocalPos;


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

    StartFiring();
  }

  internal void StopFiring()
  {
    isFiring = false;
  }

  // ---------------- LOOP ----------------

  private void Update()
  {
    if (!isFiring)
      return;

    if (awaitingHitResult)
      return;

    // 🆕 local cooldown (prevents machine gun)
    if (Time.time < nextAllowedFireTime)
      return;

    if (!IsFishValidForShot(currentTarget))
    {
      currentTarget = ResolveVariantTarget();

      if (currentTarget == null)
      {
        if (lockedVariant != null)
          return;

        StopAndClear();
        return;
      }
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

    return IsFishInViewport(fish);
  }

  private bool IsFishInViewport(BaseFish fish)
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

  // ---------------- BACKEND CALLBACK ----------------

  internal void OnFishKilled(BaseFish fish)
  {
    if (currentTarget == fish)
      currentTarget = null;
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
  }

  private void ClearLock()
  {
    currentTarget = null;
    lockedVariant = null;
  }

  internal void Awake()
  {
    initialLocalPos = transform.localPosition;
  }

  internal override void Fire() { }
}
