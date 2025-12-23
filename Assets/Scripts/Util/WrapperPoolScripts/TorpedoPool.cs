using UnityEngine;

public class TorpedoPool : GenericObjectPool<TorpedoBulletView>
{
  public static TorpedoPool Instance;

  internal override void Awake()
  { 
    base.Awake();
    Instance = this;
  }
}
