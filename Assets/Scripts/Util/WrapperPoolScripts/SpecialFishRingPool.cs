using UnityEngine;

internal class SpecialFishRingPool : GenericObjectPool<SpecialFishRingView>
{
  internal static SpecialFishRingPool Instance;

  internal override void Start()
  {
    base.Start();
    Instance = this;
  }

  internal override SpecialFishRingView GetFromPool()
  {
    if (PoolQueue.Count > 0)
    {
      var ring = PoolQueue.Dequeue();
      ItemsInUse.Add(ring);
      return ring;
    }
    return CreateNewPooledItem();
  }

  internal override SpecialFishRingView CreateNewPooledItem()
  {
    var ring = Instantiate(PrefabToPool, transform);
    if (ParentTransform != null)
      ring.transform.SetParent(ParentTransform, false);
    ring.gameObject.SetActive(false);
    ItemsInUse.Add(ring);
    return ring;
  }
}
