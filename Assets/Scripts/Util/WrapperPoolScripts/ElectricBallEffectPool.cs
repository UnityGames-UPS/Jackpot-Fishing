using UnityEngine;

internal class ElectricBallEffectPool : GenericObjectPool<ImageAnimation>
{
  internal static ElectricBallEffectPool Instance;

  internal override void Start()
  {
    base.Start();
    Instance = this;
  }
}
