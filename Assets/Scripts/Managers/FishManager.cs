using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class FishManager : MonoBehaviour
{
  internal static FishManager Instance;

  [Header("Fish Visual Definitions")]
  [SerializeField] private List<FishData> fishesData;

  [Header("Fish pools")]
  [SerializeField] internal GenericObjectPool<NormalFish> normalFishPool;
  [SerializeField] internal GenericObjectPool<GoldenFish> goldenFishPool;
  [SerializeField] internal GenericObjectPool<SpecialFish> specialFishPool;
  [SerializeField] internal GenericObjectPool<EffectFish> effectFishPool;
  [SerializeField] internal GenericObjectPool<ImmortalFish> immortalFishPool;
  [SerializeField] internal GenericObjectPool<JackpotFish> jackpotFishPool;
  [SerializeField] internal GenericObjectPool<JackpotDragon> jackpotDragonPool;
  [Header("Rock Crab Torpedo Targets")]
  [SerializeField] private Transform[] rockCrabTorpedoGridTargets;
  [SerializeField, Min(1)] private int rockCrabTorpedoGridColumns = 3;
  [SerializeField] private Vector2 rockCrabTorpedoOffsetMin = Vector2.zero;
  [SerializeField] private Vector2 rockCrabTorpedoOffsetMax = Vector2.zero;
  [Header("Fish Anim Parent")]
  [SerializeField] private Transform animParent;
  [Header("Coin Blast Scales")]
  [SerializeField] private float normalCoinBlastScale = 1f;
  [SerializeField] private float specialCoinBlastScale = 1f;
  [SerializeField] private float goldenCoinBlastScale = 1f;
  [SerializeField] private float immortalCoinBlastScale = 1f;
  [SerializeField] private float jackpotFishCoinBlastScale = 1f;
  [SerializeField] private float jackpotDragonCoinBlastScale = 1f;
  [Header("Star Coins")]
  [SerializeField] private Transform starCoinTarget;
  [SerializeField] private Transform starCoinFirstTarget;
  [SerializeField] private float starCoinSpawnRadius = 30f;
  [SerializeField] private float starCoinSpawnRadiusPerFishWidth = 0.4f;
  [SerializeField] private float starCoinSpawnRadiusMax = 120f;
  [SerializeField] private List<StarCoinSetPool> starCoinSmallPools = new List<StarCoinSetPool>();
  [SerializeField] private List<StarCoinSetPool> starCoinBigPools = new List<StarCoinSetPool>();
  [SerializeField, Min(0)] private int starCoinBlockedFishCount = 6;
  private bool starCoinTargetMissingLogged;
  [Header("Laser Impact Scales")]
  [SerializeField] private float normalLaserImpactScale = 1f;
  [SerializeField] private float specialLaserImpactScale = 1f;
  [SerializeField] private float goldenLaserImpactScale = 1f;
  [SerializeField] private float effectLaserImpactScale = 1f;
  [SerializeField] private float immortalLaserImpactScale = 1f;
  [SerializeField] private float jackpotFishLaserImpactScale = 1f;
  [SerializeField] private float jackpotDragonLaserImpactScale = 1f;
  internal Transform AnimParent => animParent;
  [SerializeField] private bool enableMockSpawning = true;
  [SerializeField] private int mockFishIndex = 25;
  [SerializeField] private List<BaseFish> activeFishes = new();
  private readonly Dictionary<BaseFish, Transform> cachedParents = new();
  private readonly Dictionary<BaseFish, int> cachedSiblingIndices = new();

  internal Transform[] RockCrabTorpedoGridTargets => rockCrabTorpedoGridTargets;
  internal int RockCrabTorpedoGridColumns => rockCrabTorpedoGridColumns;
  internal Vector2 RockCrabTorpedoOffsetMin => rockCrabTorpedoOffsetMin;
  internal Vector2 RockCrabTorpedoOffsetMax => rockCrabTorpedoOffsetMax;

  private void Awake() => Instance = this;

#if UNITY_EDITOR
  private void Update()
  {
    if (!enableMockSpawning)
      return;

    if (Input.GetKeyDown(KeyCode.Space))
    {
      SpawnMockFish();
    }
  }
#endif
  internal void SpawnMockFish()
  {
    if (fishesData == null || fishesData.Count == 0)
    {
      Debug.LogWarning("[FishManager] No fish data for mock spawn");
      return;
    }

    // FishData baseData =
    // fishesData[UnityEngine.Random.Range(0, fishesData.Count)];

    FishData baseData =
      fishesData[mockFishIndex];

    BaseFish fish = GetFishFromType(baseData.fishType);

    if (fish == null)
    {
      Debug.LogError("Fish Not Found! " + baseData.variant);
      return;
    }

    // ✅ Clone data (no backend id)
    FishData runtimeData = new FishData
    {
      variant = baseData.variant,
      animationFrames = baseData.animationFrames,
      animationSpeed = baseData.animationSpeed,
      loop = baseData.loop,
      spriteSize = baseData.spriteSize,
      colliderSize = baseData.colliderSize,
      colliderOffset = baseData.colliderOffset,
      coinBlastScaleMult = baseData.coinBlastScaleMult,
      fishType = baseData.fishType,

      fishId = null,                 // 🚫 no backend
      duration = baseData.duration
    };

    if (fish is NormalFish nf)
    {
      // ✅ NULL CONTEXT → fallback path
      nf.Initialize(runtimeData, null);
    }
    else
    {
      fish.Initialize(runtimeData);
    }

    activeFishes.Add(fish);
  }


  internal void DespawnFish(BaseFish fish)
  {
    // Debug.Log("Called");
    activeFishes.Remove(fish);

    switch (fish)
    {
      case NormalFish nf: normalFishPool.ReturnToPool(nf); break;
      case GoldenFish gf: goldenFishPool.ReturnToPool(gf); break;
      case SpecialFish sf: specialFishPool.ReturnToPool(sf); break;
      case EffectFish ef: effectFishPool.ReturnToPool(ef); break;
      case ImmortalFish im: immortalFishPool.ReturnToPool(im); break;
      case JackpotFish jf: jackpotFishPool.ReturnToPool(jf); break;
      case JackpotDragon jd: jackpotDragonPool.ReturnToPool(jd); break;
    }
  }

  internal float GetCoinBlastScale(FishType fishType)
  {
    return fishType switch
    {
      FishType.Normal => normalCoinBlastScale,
      FishType.Special => specialCoinBlastScale,
      FishType.Golden => goldenCoinBlastScale,
      FishType.Immortal => immortalCoinBlastScale,
      FishType.Jackpot_Fish => jackpotFishCoinBlastScale,
      FishType.Jackpot_Dragon => jackpotDragonCoinBlastScale,
      _ => 1f
    };
  }

  internal float GetLaserImpactScale(FishType fishType)
  {
    return fishType switch
    {
      FishType.Normal => normalLaserImpactScale,
      FishType.Special => specialLaserImpactScale,
      FishType.Golden => goldenLaserImpactScale,
      FishType.Effect => effectLaserImpactScale,
      FishType.Immortal => immortalLaserImpactScale,
      FishType.Jackpot_Fish => jackpotFishLaserImpactScale,
      FishType.Jackpot_Dragon => jackpotDragonLaserImpactScale,
      _ => 1f
    };
  }

  internal void MoveToAnimParent(BaseFish fish)
  {
    if (fish == null || animParent == null)
      return;

    if (!cachedParents.ContainsKey(fish))
    {
      cachedParents[fish] = fish.transform.parent;
      cachedSiblingIndices[fish] = fish.transform.GetSiblingIndex();
    }

    fish.transform.SetParent(animParent, true);
    fish.transform.SetAsLastSibling();
  }

  internal void RestoreFromAnimParent(BaseFish fish)
  {
    if (fish == null)
      return;

    if (!cachedParents.TryGetValue(fish, out var parent))
      return;

    fish.transform.SetParent(parent, true);
    if (cachedSiblingIndices.TryGetValue(fish, out var index) && index >= 0)
      fish.transform.SetSiblingIndex(index);

    cachedParents.Remove(fish);
    cachedSiblingIndices.Remove(fish);
  }

  private BaseFish GetFishFromType(FishType type)
  {
    return type switch
    {
      FishType.Normal => normalFishPool.GetFromPool(),
      FishType.Special => specialFishPool.GetFromPool(),
      FishType.Golden => goldenFishPool.GetFromPool(),
      FishType.Effect => effectFishPool.GetFromPool(),
      FishType.Immortal => immortalFishPool.GetFromPool(),
      FishType.Jackpot_Fish => jackpotFishPool.GetFromPool(),
      FishType.Jackpot_Dragon => jackpotDragonPool.GetFromPool(),
      _ => null
    };
  }

  internal FishData ToFishData(Fish backendFish)
  {
    var baseData = fishesData.Find(t => t.variant == backendFish.variant);
    if (baseData == null)
      return null;

    FishData runtimeData = new FishData
    {
      variant = baseData.variant,
      animationFrames = baseData.animationFrames,
      animationSpeed = baseData.animationSpeed,
      loop = baseData.loop,
      spriteSize = baseData.spriteSize,
      colliderSize = baseData.colliderSize,
      colliderOffset = baseData.colliderOffset,
      coinBlastScaleMult = baseData.coinBlastScaleMult,
      fishType = baseData.fishType,

      // backend-specific
      fishId = backendFish.id,
      duration = backendFish.lifespan
    };

    return runtimeData;
  }

  internal void TryPlayStarCoins(BaseFish fish, float winAmount, float totalBet)
  {
    if (fish.data.fishType == FishType.Immortal)
      return;

    TryPlayStarCoinsAtPosition(fish.ColliderMidPoint, winAmount, totalBet, fish);
  }

  internal void TryPlayStarCoinsAtPosition(
    Vector3 position,
    float winAmount,
    float totalBet,
    BaseFish contextFish
  )
  {
    TryPlayStarCoinsAtPosition(position, winAmount, contextFish, 0, false, StarCoinPoolMode.Default);
  }

  internal void TryPlayStarCoinsAtPosition(
    Vector3 position,
    float winAmount,
    BaseFish contextFish,
    int setCountOverride,
    bool applyScatter,
    StarCoinPoolMode poolMode
  )
  {
    if (contextFish != null && !IsStarCoinAllowedByVariant(contextFish.data?.variant))
      return;

    if (starCoinTarget == null || starCoinFirstTarget == null)
    {
      if (!starCoinTargetMissingLogged)
      {
        Debug.LogWarning("[FishManager] Star coin targets not assigned");
        starCoinTargetMissingLogged = true;
      }
      return;
    }

    if (!TryResolveStarCoinPool(winAmount, contextFish, poolMode, out var pool, out int setCount))
      return;

    if (setCountOverride > 0)
      setCount = setCountOverride;

    float scatterRadius = GetStarCoinScatterRadius(contextFish);
    for (int i = 0; i < setCount; i++)
    {
      if (pool == null)
        continue;

      var view = pool.GetFromPool();
      if (view == null)
        continue;

      Vector3 spawnPos = position;
      if (applyScatter)
      {
        Vector2 offset2D = UnityEngine.Random.insideUnitCircle * scatterRadius;
        spawnPos += new Vector3(offset2D.x, offset2D.y, 0f);
      }
      view.Play(
        spawnPos,
        starCoinFirstTarget.position,
        starCoinTarget.position,
        v => pool.ReturnToPool(v)
      );
    }
  }

  private bool TryResolveStarCoinPool(
    float winAmount,
    BaseFish contextFish,
    StarCoinPoolMode poolMode,
    out StarCoinSetPool pool,
    out int setCount
  )
  {
    pool = null;
    setCount = 0;

    if (winAmount <= 0f)
      return false;

    if (contextFish == null || contextFish.data == null)
      return false;

    setCount = 1;
    switch (poolMode)
    {
      case StarCoinPoolMode.ForceBig:
        pool = GetStarCoinPoolByIndex(starCoinBigPools, 0);
        break;
      case StarCoinPoolMode.ForceSmall:
        pool = GetRandomStarCoinPool(starCoinSmallPools);
        break;
      case StarCoinPoolMode.ForceRandom:
        pool = GetRandomStarCoinPoolFromAll();
        break;
      default:
        if (contextFish.data.fishType == FishType.Normal)
          pool = GetRandomStarCoinPool(starCoinSmallPools);
        else
          pool = GetStarCoinPoolByIndex(starCoinBigPools, 0);
        break;
    }

    return pool != null;
  }


  private StarCoinSetPool GetRandomStarCoinPool(List<StarCoinSetPool> pools)
  {
    if (pools == null || pools.Count == 0)
      return null;

    int index = UnityEngine.Random.Range(0, pools.Count);
    return pools[index];
  }

  private StarCoinSetPool GetRandomStarCoinPoolFromAll()
  {
    List<StarCoinSetPool> combined = new List<StarCoinSetPool>();
    if (starCoinSmallPools != null)
      combined.AddRange(starCoinSmallPools);
    if (starCoinBigPools != null)
      combined.AddRange(starCoinBigPools);

    return GetRandomStarCoinPool(combined);
  }

  private StarCoinSetPool GetStarCoinPoolByIndex(List<StarCoinSetPool> pools, int index)
  {
    if (pools == null || pools.Count == 0)
      return null;

    int safeIndex = Mathf.Clamp(index, 0, pools.Count - 1);
    return pools[safeIndex];
  }

  private float GetStarCoinScatterRadius(BaseFish fish)
  {
    float radius = Mathf.Max(0f, starCoinSpawnRadius);
    if (fish == null || fish.data == null)
      return Mathf.Min(radius, starCoinSpawnRadiusMax);

    if (fish.data.fishType != FishType.Jackpot_Dragon && fish.Rect != null)
    {
      float width = fish.Rect.rect.width;
      radius += width * Mathf.Max(0f, starCoinSpawnRadiusPerFishWidth);
    }

    return Mathf.Min(radius, starCoinSpawnRadiusMax);
  }

  internal enum StarCoinPoolMode
  {
    Default,
    ForceBig,
    ForceSmall,
    ForceRandom
  }


  private bool IsStarCoinAllowedByVariant(string variant)
  {
    if (string.IsNullOrEmpty(variant))
      return true;

    if (fishesData == null || fishesData.Count == 0)
      return true;

    int blockCount = Mathf.Clamp(starCoinBlockedFishCount, 0, fishesData.Count);
    for (int i = 0; i < blockCount; i++)
    {
      if (fishesData[i] != null && fishesData[i].variant == variant)
        return false;
    }

    return true;
  }


  private FishType ParseFishType(string type)
  {
    return type.ToLower() switch
    {
      "normal" => FishType.Normal,
      "special" => FishType.Special,
      "golden" => FishType.Golden,
      "effect" => FishType.Effect,
      "immortal" => FishType.Immortal,
      "jackpot_fish" => FishType.Jackpot_Fish,
      "jackpot_dragon" => FishType.Jackpot_Dragon,
      _ => FishType.Normal
    };
  }

  internal BaseFish SpawnFishFromBackend(FishData data, SpawnBatchContext context)
  {
    BaseFish fish = GetFishFromType(data.fishType);

    if (fish == null)
    {
      Debug.LogError("Fish Not Found! " + data.variant);
      return null;
    }

    if (fish is NormalFish nf)
    {
      nf.Initialize(data, context);
    }
    else
      fish.Initialize(data);

    activeFishes.Add(fish);
    return fish;
  }



  internal IReadOnlyList<BaseFish> GetActiveFishes()
  {
    return activeFishes;
  }

}

[Serializable]
public class FishData
{
  public string variant;                 // e.g. "angelfish"
  public string fishId;
  public Sprite[] animationFrames;
  public float animationSpeed = 5f;
  public bool loop = true;
  public Vector2 spriteSize;
  public Vector2 colliderSize;
  public Vector2 colliderOffset;
  public float coinBlastScaleMult = 1.8f;
  public int duration = 10000;
  public FishType fishType = FishType.Normal;
}

public enum FishType
{
  Normal,
  Golden,
  Special,
  Effect,
  Immortal,
  Jackpot_Fish,
  Jackpot_Dragon
}
