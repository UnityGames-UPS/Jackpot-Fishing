using DG.Tweening;
using UnityEngine;

internal class CoinBlastAnimPool : GenericObjectPool<ImageAnimation>
{
  internal static CoinBlastAnimPool Instance;

  internal override void Awake()
  {
    base.Awake();
    Instance = this;
  }

  internal override ImageAnimation GetFromPool()
  {
    var anim = base.GetFromPool();
    anim.OnAnimationComplete = () =>
    {
      anim.gameObject.SetActive(false); 
    };
    return anim;
  }
}
