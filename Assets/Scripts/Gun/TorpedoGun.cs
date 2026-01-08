using UnityEngine;

public class TorpedoGun : BaseGun
{
  [SerializeField] internal float FireInterval = 0.6f;

  private float lastFireTime;
  private bool isFiring;

  private BaseFish lockedFish;
  private string lockedVariant;

  // ---------------- INPUT ENTRY ----------------
  internal void HandlePointerDown(BaseFish hitFish)
  {
    if (!UIManager.Instance.IsValidTorpedoTarget(hitFish))
    {
      ClearCurrentLock();
      return;
    }

    if (hitFish == null)
    {
      ClearCurrentLock();
      return;
    }

    if (lockedVariant != null &&
        hitFish.data.variant != lockedVariant)
    {
      ClearCurrentLock();
      return;
    }

    if (lockedVariant == null)
    {
      lockedFish = hitFish;
      lockedVariant = hitFish.data.variant;
    }

    StartFiring();
  }

  internal void UpdateUnlockedFire(BaseFish hitFish)
  {
    if (!UIManager.Instance.IsValidTorpedoTarget(hitFish))
    {
      StopFiring();
      return;
    }

    StartFiring();
  }

  internal void StopFiring()
  {
    isFiring = false;
  }


  internal void ClearCurrentLock()
  {
    lockedFish = null;
    lockedVariant = null;
  }

  // ---------------- LOOP ----------------

  private void Update()
  {
    if (!isFiring)
      return;


    if (Time.time - lastFireTime < FireInterval)
      return;

    BaseFish target;

    if (UIManager.Instance.IsTargetLockEnabled)
    {
      target = ResolveTarget();
    }
    else
    {
      target = InputManagerView.Instance.GetCurrentPointerFish();
    }

    if (target == null)
    {
      StopFiring();
      return;
    }

    if (!UIManager.Instance.IsValidTorpedoTarget(target))
    {
      StopFiring();
      return;
    }

    TryFire(target);
  }

  internal void StartFiring()
  {
    isFiring = true;
  }

  private bool IsFishValidForLock(BaseFish fish)
  {
    if (fish == null || !fish.gameObject.activeInHierarchy)
      return false;

    var cam = Camera.main;
    var bounds = fish.GetComponent<BoxCollider2D>().bounds;

    Vector3 min = cam.WorldToViewportPoint(bounds.min);
    Vector3 max = cam.WorldToViewportPoint(bounds.max);

    // any overlap with screen
    return max.x > 0 && min.x < 1 &&
           max.y > 0 && min.y < 1 &&
           max.z > 0;
  }

  // ---------------- TARGET RESOLUTION ----------------

  private BaseFish ResolveTarget()
  {
    if (lockedVariant == null)
      return null;

    // Prefer locked fish if still alive
    if (lockedFish != null &&
        lockedFish.gameObject.activeInHierarchy)
      return lockedFish;

    // Find another fish of same variant
    foreach (var fish in FishManager.Instance.GetActiveFishes())
    {
      if (fish != null &&
          fish.data != null &&
          fish.data.variant == lockedVariant)
      {
        lockedFish = fish;
        return fish;
      }
    }

    // No fish of this variant left
    ClearCurrentLock();
    return null;
  }

  // ---------------- FIRE ----------------

  private void TryFire(BaseFish fish)
  {
    if (!UIManager.Instance.OnGunFired())
      return;

    TorpedoBulletView torpedo = TorpedoPool.Instance.GetFromPool();
    torpedo.transform.SetPositionAndRotation(muzzle.position, Quaternion.identity);

    Vector3 dir = (fish.transform.position - muzzle.position).normalized;
    torpedo.Init(fish, dir);

    lastFireTime = Time.time;
  }



  internal override void Fire() { }
}
