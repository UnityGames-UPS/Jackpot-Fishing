using UnityEngine;

public class BubbleWhirlpoolPool : GenericObjectPool<BubbleWhirlpoolBubble>
{
  private bool initialized;

  internal void Configure(BubbleWhirlpoolBubble prefab, Transform parent, int initialCount)
  {
    if (initialized)
      return;

    if (prefab == null)
    {
      Debug.LogError("[BubbleWhirlpoolPool] Missing bubble prefab.");
      return;
    }

    PrefabToPool = prefab;
    ParentTransform = parent;
    InitializePool(Mathf.Max(0, initialCount));
    initialized = true;
  }

  internal override BubbleWhirlpoolBubble GetFromPool()
  {
    if (!initialized)
    {
      Debug.LogError("[BubbleWhirlpoolPool] Pool not configured.");
      return null;
    }

    return base.GetFromPool();
  }
}
