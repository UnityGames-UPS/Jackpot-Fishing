using UnityEngine;

internal class ElectricLinkEffectPool : GenericObjectPool<ElectricLinkEffectView>
{
  internal static ElectricLinkEffectPool Instance;

  internal override void Awake()
  {
    base.Awake();
    Instance = this;
  }
}
