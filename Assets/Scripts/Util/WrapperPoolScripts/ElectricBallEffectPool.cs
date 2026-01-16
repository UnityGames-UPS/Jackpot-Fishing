using UnityEngine;

internal class ElectricBallEffectPool : GenericObjectPool<ImageAnimation>
{
  internal static ElectricBallEffectPool Instance;

  internal override void Awake()
  {
    base.Awake();
    Instance = this;
  }
}
