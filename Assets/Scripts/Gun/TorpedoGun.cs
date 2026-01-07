using UnityEngine;

public class TorpedoGun : BaseGun
{
  internal override float FireInterval => 6f;

  internal override void Awake()
  {
    base.Awake();
  }

  internal void FireTorpedo(BaseFish fish)
  {
    if (fish == null)
      return;

    TorpedoBulletView torpedo = TorpedoPool.Instance.GetFromPool();
    torpedo.transform.SetPositionAndRotation(muzzle.position, Quaternion.identity);
    Vector3 dir = (fish.transform.position - muzzle.position).normalized;
    torpedo.Init(fish, dir);
  }

  internal override void Fire()
  {
    
  }
}
