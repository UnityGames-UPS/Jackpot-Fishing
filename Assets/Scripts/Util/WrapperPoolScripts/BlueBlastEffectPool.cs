using UnityEngine;

internal class BlueBlastEffectPool : GenericObjectPool<ImageAnimation>
{
  internal static BlueBlastEffectPool Instance;

  internal override void Start()
  {
    base.Start();
    Instance = this;
  }
}
