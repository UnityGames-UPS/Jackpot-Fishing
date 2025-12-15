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
  [SerializeField] internal GenericObjectPool<SpecialFish> specialFishPool;
  [SerializeField] internal GenericObjectPool<ImmortalFish> immortalFishPool;
  [SerializeField] internal GenericObjectPool<JackpotFish> jackpotFishPool;

  [Header("MockData")]
  [SerializeField] private List<FishData> mockFishData;

  private void Awake() => Instance = this;

  void Update()
  {
    if (Input.GetKeyDown(KeyCode.Space))
    {
      SpawnFish();
    }
  }

  internal void SpawnFish()
  {
    int randomIndex = UnityEngine.Random.Range(0, mockFishData.Count);
    Fish fish = GetFishFromType(mockFishData[randomIndex].FishType);
    if (fish == null) return;

    fish.Initialize(mockFishData[randomIndex]);
  }

  internal void DespawnFish(Fish fish)
  {
    fish.ResetFish();
    switch (fish)
    {
      case NormalFish nf: normalFishPool.ReturnToPool(nf); break;
      case SpecialFish sf: specialFishPool.ReturnToPool(sf); break;
      case ImmortalFish im: immortalFishPool.ReturnToPool(im); break;
      case JackpotFish jf: jackpotFishPool.ReturnToPool(jf); break;
    }
  }

  private Fish GetFishFromType(FishType type)
  {
    return type switch
    {
      FishType.Normal => normalFishPool.GetFromPool(),
      FishType.Special => specialFishPool.GetFromPool(),
      FishType.Immortal => immortalFishPool.GetFromPool(),
      FishType.Jackpot => jackpotFishPool.GetFromPool(),
      _ => null
    };
  }

  internal FishVisualData GetVisualData(string fishId)
  {
    return fishVisuals.Find(v => v.fishId == fishId);
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
}

public enum FishType
{
  Normal,
  Special,
  Immortal,
  Jackpot
}
