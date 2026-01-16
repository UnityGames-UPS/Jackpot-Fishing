using UnityEngine;

internal class BlueBlastEffectPool : GenericObjectPool<ImageAnimation>
{
  internal static BlueBlastEffectPool Instance;

  internal override void Awake()
  {
    base.Awake();
    Instance = this;
  }
}
