using UnityEngine;

public class TorpedoPool : GenericObjectPool<TorpedoBulletView>
{
  public static TorpedoPool Instance;

  internal override void Start()
  { 
    base.Start();
    Instance = this;
  }
}
