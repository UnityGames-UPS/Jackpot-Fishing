using UnityEngine;

public class CrabTorpedoPool : GenericObjectPool<CrabTorpedoBulletView>
{
  public static CrabTorpedoPool Instance;

  internal override void Start()
  {
    base.Start();
    Instance = this;
  }
}
