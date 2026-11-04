using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// PAUSED (old backend). The old spawn and hit-result handling, moved verbatim out of
// SocketIOManager when the client was ported to the ML-FG backend. Nothing calls this yet;
// it is kept compiling as the reference for re-enabling torpedo, laser, effect-fish chains,
// immortal buckets and rainbow wins once the new backend supports them.
internal class LegacyHitResultHandler
{
  private readonly MonoBehaviour host;
  private readonly float effectFishExpireWait;
  private readonly Dictionary<string, Coroutine> effectFishExpireRoutines = new Dictionary<string, Coroutine>();

  internal LegacyHitResultHandler(MonoBehaviour host, float effectFishExpireWait = 2f)
  {
    this.host = host;
    this.effectFishExpireWait = effectFishExpireWait;
  }

  internal IEnumerator SpawnFishes(List<Fish> fishes)
  {
    if (fishes == null || fishes.Count <= 0)
    {
      Debug.LogError("No fishes found in payload");
      yield break;
    }

    List<FishData> fishesData = new();
    fishes.ForEach((t) =>
    {
      FishData fishData = FishManager.Instance.ToFishData(t);
      if (fishData == null) Debug.LogError("Fish data not found for: " + t.variant);
      fishesData.Add(fishData);
    });

    if (fishesData.Count <= 0)
    {
      Debug.LogError("no fish data found in list");
      yield break;
    }

    foreach (FishData data in fishesData)
      FishManager.Instance.SpawnFishFromBackend(data);
  }

  internal void HandleHitResult(Root root)
  {
    Payload hitResult = root.payload;

    if (GunManager.Instance.currentGun is LazerGun lazerGun)
    {
      lazerGun.OnHitResult();
    }

    if (hitResult == null)
    {
      Debug.LogError("Null hit result");
      return;
    }

    if (hitResult.isExpired && !string.IsNullOrEmpty(hitResult.fishId))
    {
      Debug.Log("Expired fish cleanup: " + hitResult.fishId);
      BaseFish expiredFish = FishManager.Instance
        .GetActiveFishes()
        .FirstOrDefault(x => x.data.fishId == hitResult.fishId);

      if (expiredFish != null)
      {
        if (expiredFish is EffectFish effectFish)
        {
          StartEffectFishExpireWait(effectFish.data.fishId, effectFish);
          return;
        }
        expiredFish.Die();
      }
    }

    // Debug.Log("HIT RESULT:" + JsonConvert.SerializeObject(HitResult));
    if (!root.success)
    {
      if (!string.IsNullOrEmpty(hitResult.message) &&
          hitResult.message.Contains("amount refunded", StringComparison.OrdinalIgnoreCase))
      {
        UIManager.Instance?.PlayRefundText(hitResult.totalBet);
      }
      return;
    }

    if (hitResult.winAmount > 0 && FishManager.Instance != null)
    {
      string targetFishId = hitResult.hitFish != null ? hitResult.hitFish.id : null;
      if (string.IsNullOrEmpty(targetFishId) && hitResult.fishKilled != null)
        targetFishId = hitResult.fishKilled.id;

      BaseFish targetFish = null;
      if (!string.IsNullOrEmpty(targetFishId))
      {
        targetFish = FishManager.Instance
          .GetActiveFishes()
          .FirstOrDefault(x => x.data != null && x.data.fishId == targetFishId);
      }

      if (targetFish != null && targetFish.data != null &&
          targetFish.data.fishType != FishType.Normal &&
          targetFish.data.fishType != FishType.Jackpot_Dragon)
      {
        if (targetFish is EffectFish effectFish)
        {
          effectFish.SetPendingWinAmount(hitResult.winAmount);
        }
        else
        {
          if (hitResult.weaponType == "torpedo")
          {
            targetFish.WaitForLastTorpedo(
              () => UIManager.Instance?.PlayRainbowWinAnimation(
                targetFish,
                targetFish.data,
                hitResult.winAmount
              ),
              0f
            );
          }
          else
          {
            UIManager.Instance?.PlayRainbowWinAnimation(
              targetFish,
              targetFish.data,
              hitResult.winAmount
            );
          }
        }
      }

      if (targetFish is ImmortalFish immortalFish &&
          (targetFish.data.variant == "golden_shark" ||
           targetFish.data.variant == "king_squid"))
      {
        immortalFish.TriggerWinAnimSpeedBoost();
      }
    }

    if (hitResult.winAmount > 0 &&
        hitResult.hitFish != null &&
        hitResult.hitFish.variant == "treasure_bowl" &&
        FishManager.Instance != null)
    {
      BaseFish turtleFish = FishManager.Instance
        .GetActiveFishes()
        .FirstOrDefault(x => x.data != null && x.data.fishId == hitResult.hitFish.id);

      if (turtleFish is ImmortalFish immortalFish && !immortalFish.BucketAnimPlaying)
      {
        var pool = ImmortalBucketAnimPool.Instance;
        if (pool != null)
        {
          var bucketAnim = pool.GetFromPool();
          if (bucketAnim != null)
          {
            immortalFish.SetBucketAnimPlaying(true);
            bucketAnim.Play(
              turtleFish.ColliderMidPoint,
              () => immortalFish.SetBucketAnimPlaying(false)
            );
          }
        }
      }
    }

    if (hitResult.fishKilled != null)
    {
      Debug.Log("Fish Killed: " + hitResult.fishKilled.variant);
      CancelEffectFishExpireWait(hitResult.fishKilled.id);
      BaseFish fish = FishManager.Instance
        .GetActiveFishes()
        .FirstOrDefault(x => x.data.fishId == hitResult.fishKilled.id);

      if (fish == null)
      {
        Debug.LogError("No alive fish found to kill. " + hitResult.fishKilled.variant + " " + hitResult.fishKilled.id);
        return;
      }

      EffectFish effectFish = fish as EffectFish;
      bool isBubbleCrab = effectFish != null &&
        fish.data != null &&
        fish.data.variant == "sea_anemone";
      bool isBlueFish = effectFish != null &&
        fish.data != null &&
        fish.data.variant == "thunder_shark";
      bool isRockCrab = effectFish != null &&
        fish.data != null &&
        fish.data.variant == "torpedo_crab";

      if (effectFish != null)
        effectFish.SetPendingEffectBonusWins(hitResult.effectTriggered?.bonusWins, hitResult.totalBet);

      if (fish.data != null &&
          fish.data.fishType != FishType.Immortal &&
          fish.data.fishType != FishType.Effect)
      {
        if (hitResult.weaponType == "torpedo")
        {
          fish.WaitForLastTorpedo(
            () => FishManager.Instance?.PlayStarCoinsForFish(fish),
            0f
          );
        }
        else
        {
          FishManager.Instance?.PlayStarCoinsForFish(fish);
        }
      }

      if (!isBubbleCrab && !isBlueFish && !isRockCrab)
      {
        // if(HitResult.winAmount > UIManager.Instance.currentBet * UIManager.Instance.GetGunCost())
        fish.OnFishDespawned = () => UIManager.Instance?.PlayCoinBlastForFish(fish);
      }

      TorpedoGun torpedoGun = null;
      if (GunManager.Instance?.Guns != null)
        torpedoGun = GunManager.Instance.Guns.OfType<TorpedoGun>().FirstOrDefault();

      bool isLocalTorpedoKill = false;
      if (torpedoGun != null && hitResult.weaponType == "torpedo")
      {
        isLocalTorpedoKill = torpedoGun.GetLockedFish() == fish;
      }

      bool isVisibleForTorpedo = IsFishVisibleForTorpedo(fish);

      if (hitResult.weaponType == "torpedo")
        torpedoGun?.OnFishKilled(fish);
      fish.MarkPendingDeath();

      if (isBubbleCrab)
      {
        List<BaseFish> affectedFishes = ResolveAffectedFishes(hitResult.effectTriggered);
        if (hitResult.weaponType == "torpedo")
        {
          effectFish.WaitForLastTorpedo(
            () => effectFish.TriggerBubbleCrabDeath(affectedFishes),
            3f
          );
        }
        else
        {
          effectFish.TriggerBubbleCrabDeath(affectedFishes);
        }
        return;
      }

      if (isBlueFish)
      {
        List<BaseFish> affectedFishes = ResolveAffectedFishes(hitResult.effectTriggered);
        if (hitResult.weaponType == "torpedo")
        {
          effectFish.WaitForLastTorpedo(
            () => effectFish.TriggerBlueFishDeath(affectedFishes),
            3f
          );
        }
        else
        {
          effectFish.TriggerBlueFishDeath(affectedFishes);
        }
        return;
      }

      if (isRockCrab)
      {
        List<BaseFish> affectedFishes = ResolveAffectedFishes(hitResult.effectTriggered);
        if (hitResult.weaponType == "torpedo")
        {
          effectFish.WaitForLastTorpedo(
            () => effectFish.TriggerRockCrabDeath(affectedFishes),
            3f
          );
        }
        else
        {
          effectFish.TriggerRockCrabDeath(affectedFishes);
        }
        return;
      }

      if (fish is JackpotDragon jackpotDragon)
      {
        if (hitResult.weaponType == "torpedo")
        {
          fish.WaitForLastTorpedo(
            () => jackpotDragon.TriggerDeathSequence(hitResult.winAmount),
            3f
          );
        }
        else
        {
          jackpotDragon.TriggerDeathSequence(hitResult.winAmount);
        }
        return;
      }

      // Weapon-aware death handling
      if (hitResult.weaponType == "torpedo")
      {
        fish.deathCause = BaseFish.DeathCause.Torpedo;
        fish.WaitForLastTorpedo(
          () => fish.Die(),
          3f
        );
      }
      else
      {
        fish.deathCause =
          hitResult.weaponType == "lazer"
            ? BaseFish.DeathCause.Lazer
            : BaseFish.DeathCause.Bullet;

        // Instant despawn for non-torpedo weapons
        fish.Die();
      }
    }
  }

  private bool IsFishVisibleForTorpedo(BaseFish fish)
  {
    if (fish == null)
      return false;

    return fish.IsVisibleInViewport;
  }

  private void StartEffectFishExpireWait(string fishId, EffectFish fish)
  {
    if (string.IsNullOrEmpty(fishId) || fish == null)
      return;
    if (fish.isDespawning || fish.finalized)
      return;
    if (effectFishExpireRoutines.ContainsKey(fishId))
      return;

    effectFishExpireRoutines[fishId] = host.StartCoroutine(EffectFishExpireWaitRoutine(fishId, fish));
  }

  private void CancelEffectFishExpireWait(string fishId)
  {
    if (string.IsNullOrEmpty(fishId))
      return;

    if (effectFishExpireRoutines.TryGetValue(fishId, out var routine))
    {
      if (routine != null)
        host.StopCoroutine(routine);
      effectFishExpireRoutines.Remove(fishId);
    }
  }

  private IEnumerator EffectFishExpireWaitRoutine(string fishId, EffectFish fish)
  {
    yield return new WaitForSeconds(effectFishExpireWait);

    effectFishExpireRoutines.Remove(fishId);

    if (fish == null || fish.finalized || fish.isDespawning)
      yield break;

    fish.Die();
  }

  private List<BaseFish> ResolveAffectedFishes(EffectTriggered effectTriggered)
  {
    List<BaseFish> affected = new List<BaseFish>();
    if (effectTriggered?.affectedFish == null)
      return affected;

    foreach (var entry in effectTriggered.affectedFish)
    {
      if (string.IsNullOrEmpty(entry.id))
        continue;

      BaseFish fish = FishManager.Instance
        .GetActiveFishes()
        .FirstOrDefault(x => x.data != null && x.data.fishId == entry.id);

      if (fish != null)
        affected.Add(fish);
    }

    return affected;
  }
}
