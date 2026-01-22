using UnityEngine;

// SpecialFish class
internal class SpecialFish : BaseFish
{
  [SerializeField] private Vector3 ringOffset = Vector3.zero;
  private SpecialFishRingView ringView;

  internal override void Initialize(FishData data)
  {
    base.Initialize(data);
    SetupFallbackMovement();
    Invoke("AttachRing", 0.1f);
  }

  internal override void ResetFish()
  {
    ReturnRingToPool();
    base.ResetFish();
  }

  private void OnDisable()
  {
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
    ringView.AttachToFish(this, ringOffset);
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
