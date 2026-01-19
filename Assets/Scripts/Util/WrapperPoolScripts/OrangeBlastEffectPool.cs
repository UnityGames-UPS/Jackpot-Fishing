using UnityEngine;

internal class OrangeBlastEffectPool : GenericObjectPool<ImageAnimation>
{
  internal static OrangeBlastEffectPool Instance;

  internal override void Start()
  {
    base.Start();
    Instance = this;
  }
}
