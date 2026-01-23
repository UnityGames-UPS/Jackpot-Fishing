using UnityEngine;

// SpecialFish class
internal class SpecialFish : BaseFish
{
  private SpecialFishRingView ringView;

  internal override void Initialize(FishData data)
  {
    base.Initialize(data);
    SetupFallbackMovement();
    Invoke(nameof(AttachRing), 0.1f);
  }

  protected override void DespawnFish()
  {
    base.DespawnFish();
    ReturnRingToPool();
  }


  private void AttachRing()
  {
    var pool = SpecialFishRingPool.Instance;
    if (pool == null)
      return;

    ringView = pool.GetFromPool();
    if (ringView == null)
      return;

    ringView.gameObject.SetActive(false);
    ringView.AttachToFish(this);
    ringView.gameObject.SetActive(true);
  }

  private void ReturnRingToPool()
  {
    if (ringView == null)
      return;

    ringView.ReturnToPool();
    ringView = null;
  }
}
