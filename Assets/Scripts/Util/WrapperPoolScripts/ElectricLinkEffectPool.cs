using UnityEngine;

internal class ElectricLinkEffectPool : GenericObjectPool<ElectricLinkEffectView>
{
  internal static ElectricLinkEffectPool Instance;

  internal override void Start()
  {
    base.Start();
    Instance = this;
  }
}
