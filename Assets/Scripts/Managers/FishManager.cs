using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class FishManager : MonoBehaviour
{
  public static FishManager Instance;

  [Header("Fish Visual Definitions")]
  [SerializeField] private List<FishVisualData> fishVisuals;

  [Header("Fish pools")]
  [SerializeField] internal GenericObjectPool<NormalFish> normalFishPool;
  [SerializeField] internal GenericObjectPool<GoldenFish> goldenFishPool;
  [SerializeField] internal GenericObjectPool<SpecialFish> specialFishPool;
  [SerializeField] internal GenericObjectPool<EffectFish> effectFishPool;
  [SerializeField] internal GenericObjectPool<ImmortalFish> immortalFishPool;
  [SerializeField] internal GenericObjectPool<JackpotFish> jackpotFishPool;
  [SerializeField] internal GenericObjectPool<JackpotDragon> jackpotDragonPool;

  [Header("MockData")]
  [SerializeField] private List<FishData> mockFishData;
  [SerializeField] private List<FishData> mockFishData2;
  [SerializeField] private List<FishData> mockFishData3;

  private void Awake() => Instance = this;

  void Update()
  {
    if (Input.GetKeyDown(KeyCode.Space))
    {
      SpawnMockFish();
    }
  }

  internal void SpawnMockFish()
  {
    int randomIndex = UnityEngine.Random.Range(0, mockFishData.Count);
    BaseFish fish = GetFishFromType(mockFishData[randomIndex].FishType);
    if (fish == null)
    {
      Debug.LogError("Fish Not Found! " + mockFishData[randomIndex].fishId);
      return;
    }

    fish.Initialize(mockFishData[randomIndex]);
  }

  internal void DespawnFish(BaseFish fish)
  {
    fish.ResetFish();
    switch (fish)
    {
      case NormalFish nf: normalFishPool.ReturnToPool(nf); break;
      case GoldenFish gf: goldenFishPool.ReturnToPool(gf); break;
      case SpecialFish sf: specialFishPool.ReturnToPool(sf); break;
      case EffectFish ef: effectFishPool.ReturnToPool(ef); break;
      case ImmortalFish im: immortalFishPool.ReturnToPool(im); break;
      case JackpotFish jf: jackpotFishPool.ReturnToPool(jf); break;
    }
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

  internal FishVisualData GetVisualData(string fishId)
  {
    return fishVisuals.Find(v => v.fishId == fishId);
  }

  internal FishData ToFishData(Fish backendFish)
  {
    return new FishData
    {
      fishId = backendFish.variant,              // 🔴 maps to visuals
      FishType = ParseFishType(backendFish.type),
      minInterval = backendFish.lifespan,        // ms → already used
      pathId = null                              // random for now
    };
  }

  private FishType ParseFishType(string type)
  {
    return type.ToLower() switch
    {
      "normal" => FishType.Normal,
      "golden" => FishType.Golden,
      "special" => FishType.Special,
      "effect" => FishType.Effect,
      "jackpot_fish" => FishType.Jackpot_Fish,
      "jackpot_dragon" => FishType.Jackpot_Dragon,
      _ => FishType.Normal
    };
  }

  internal BaseFish SpawnFishFromBackend(FishData data)
  {
    BaseFish fish = GetFishFromType(data.FishType);
    if (fish == null)
      return null;

    fish.Initialize(data);
    return fish;
  }
}

[Serializable]
internal class FishData
{
  //backend sends
  public string fishId;
  // public string type;
  // public string variants;
  public int minInterval;
  // public string direction;
  public string pathId;

  //unity references
  public FishType FishType;
}

[Serializable]
internal class FishVisualData
{
  public string fishId;                 // e.g. "angelfish"
  public Sprite[] animationFrames;
  public float animationSpeed = 5f;
  public bool loop = true;
  public Vector2 spriteSize;
  public Vector2 colliderSize;
  public Vector2 colliderOffset;
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
