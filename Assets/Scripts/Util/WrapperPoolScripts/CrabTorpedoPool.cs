using UnityEngine;

public class CrabTorpedoPool : GenericObjectPool<CrabTorpedoBulletView>
{
  public static CrabTorpedoPool Instance;

  internal override void Awake()
  {
    base.Awake();
    Instance = this;
  }
}
