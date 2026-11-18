using System;
using UnityEngine;

// The normal gun's art level, picked by the bet: the hall's bets list is split into three equal parts.
internal enum GunTier { Base, Buff, Buffed }

internal static class GunTiers
{
  private const int Count = 3;

  // 10 bets -> 4/3/3. Fewer than 3 bets skip tiers evenly.
  internal static GunTier For(int betIndex, int betCount)
  {
    if (betCount <= 0)
      return GunTier.Base;
    return (GunTier)Mathf.Clamp(betIndex * Count / betCount, 0, Count - 1);
  }

  // The current hall's split, shared by every caller.
  internal static GunTier ForBet(int betIndex) =>
    For(betIndex, SocketIOManager.Instance?.bets?.Count ?? 0);
}

// One sprite per tier. An empty slot means "keep the current art" (art still pending).
[Serializable]
internal struct TierSprites
{
  [SerializeField] private Sprite baseSprite;
  [SerializeField] private Sprite buffSprite;
  [SerializeField] private Sprite buffedSprite;

  internal Sprite Get(GunTier tier) => tier switch
  {
    GunTier.Buff => buffSprite,
    GunTier.Buffed => buffedSprite,
    _ => baseSprite,
  };

  internal void ApplyTo(UnityEngine.UI.Image image, GunTier tier)
  {
    Sprite sprite = Get(tier);
    if (image != null && sprite != null)
      image.sprite = sprite;
  }
}
