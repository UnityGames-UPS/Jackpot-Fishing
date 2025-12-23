using UnityEngine;

public class TorpedoGun : BaseGun
{
  [Range(0.1f, 1f)]
  [SerializeField] private float BlastScaleFactor = 0.7f;
  internal override float FireInterval => 6f;

  internal override void Awake()
  {
    base.Awake();
  }

  internal void FireTorpedo(Fish fish)
  {
    if (fish == null)
      return;

    TorpedoBulletView torpedo = TorpedoPool.Instance.GetFromPool();
    torpedo.transform.SetPositionAndRotation(muzzle.position, Quaternion.identity);
    torpedo.Init(fish, BlastScaleFactor);
  }

  internal override void Fire()
  {
    
  }
}
