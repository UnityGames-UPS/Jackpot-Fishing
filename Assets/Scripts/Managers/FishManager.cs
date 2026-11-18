using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

internal class FishManager : MonoBehaviour
{
  internal static FishManager Instance;

  [Header("Editor Mock (M spawns, K kills, F toggles a room freeze)")]
  [SerializeField] private bool enableMockSpawning = true;
  [SerializeField] private int mockFishIndex = 25;
  [Tooltip("Optional: Config.json. Mock fish then use its hitboxRadius/segments per type (for tuning hitboxes).")]
  [SerializeField] private TextAsset mockConfig;
  private Dictionary<string, MockHitbox> mockHitboxes;

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
  [Header("Laser Impact Scales")]
  [SerializeField] private float normalLaserImpactScale = 1f;
  [SerializeField] private float specialLaserImpactScale = 1f;
  [SerializeField] private float goldenLaserImpactScale = 1f;
  [SerializeField] private float effectLaserImpactScale = 1f;
  [SerializeField] private float immortalLaserImpactScale = 1f;
  [SerializeField] private float jackpotFishLaserImpactScale = 1f;
  [SerializeField] private float jackpotDragonLaserImpactScale = 1f;
  [Header("Star Coins")]
  [SerializeField] private Transform starCoinTarget;
  [SerializeField] private Transform starCoinFirstTarget;
  [SerializeField] private StarCoinSetPool starCoinBigPool;
  [SerializeField] private StarCoinSetPool starCoinSmallPoolA;
  [SerializeField] private StarCoinSetPool starCoinSmallPoolB;
  [SerializeField, Min(0)] private int normalSmallPoolAThreshold = 3;
  [SerializeField, Min(0)] private int normalSmallPoolBThreshold = 5;
  [SerializeField, Min(0)] private int normalBigPoolThreshold = 8;
  [SerializeField, Min(0f)] private float starCoinDelay = 0.5f;
  [SerializeField] private float effectStarCoinOffsetRadius = 30f;
  [SerializeField] private float effectStarCoinOffsetRadiusPerFishWidth = 0.4f;
  [SerializeField] private float effectStarCoinOffsetRadiusMax = 120f;
  private bool starCoinTargetMissingLogged;
  internal Transform AnimParent => animParent;

  [Header("Playfield")]
  [Tooltip("Fixed 1920x1080 rect (Parent1) that server coordinates map onto.")]
  [SerializeField] private RectTransform playfieldRect;

  [SerializeField] private List<BaseFish> activeFishes = new();
  private readonly Dictionary<string, BaseFish> fishById = new();
  private readonly Dictionary<BaseFish, Transform> cachedParents = new();
  private readonly Dictionary<BaseFish, int> cachedSiblingIndices = new();
  private readonly HashSet<string> warnedVariants = new();
  private int mockCounter;

  // Where recently released fish were, so a shot_result that arrives after the
  // fish is gone can still play its payout at the right spot.
  private const float RecentlyRemovedTtl = 5f;
  private readonly Dictionary<string, RemovedFish> recentlyRemoved = new();
  private readonly Queue<(string id, float time)> recentlyRemovedOrder = new();

  private struct RemovedFish
  {
    internal Vector3 position;
    internal FishData data;
  }

  internal Transform[] RockCrabTorpedoGridTargets => rockCrabTorpedoGridTargets;
  internal int RockCrabTorpedoGridColumns => rockCrabTorpedoGridColumns;
  internal Vector2 RockCrabTorpedoOffsetMin => rockCrabTorpedoOffsetMin;
  internal Vector2 RockCrabTorpedoOffsetMax => rockCrabTorpedoOffsetMax;

  private void Awake()
  {
    Instance = this;

    if (playfieldRect != null)
      Playfield.Init(playfieldRect);
    else
      GameLog.Error(LogCategory.Fish, "FishManager.playfieldRect is not assigned. Server fish can't be positioned.");
  }

#if UNITY_EDITOR
  private void Update()
  {
    if (!enableMockSpawning)
      return;

    if (Input.GetKeyDown(KeyCode.M))
      SpawnMockFish();

    if (Input.GetKeyDown(KeyCode.K))
      MockKillRandomFish();

    if (Input.GetKeyDown(KeyCode.F))
      MockToggleFreeze();
  }
#endif

  // --------------------------------------------------------
  // SERVER-DRIVEN FISH (ML-FG)
  // --------------------------------------------------------

  internal BaseFish SpawnFromServer(SpawnedFish dto)
  {
    if (dto == null || string.IsNullOrEmpty(dto.id))
      return null;

    if (!Playfield.IsReady)
    {
      GameLog.Error(LogCategory.Fish, WithFishJson("Playfield not initialized; dropping fish " + dto.id, dto));
      return null;
    }

    // init.activeFish and game:fish_spawned can overlap on join.
    if (fishById.ContainsKey(dto.id))
      return null;

    var path = new ServerPath(dto);
    if (!path.IsValid)
    {
      GameLog.Warn(LogCategory.Fish, WithFishJson($"Fish {dto.id} ({dto.type}) has no usable path", dto));
      return null;
    }

    // Not expected; such a fish ignores the freeze (RoomFreeze.FishTime) and still spawns.
    if (RoomFreeze.IsFrozen && path.SpawnTime >= RoomFreeze.StartMs)
      GameLog.Warn(LogCategory.Fish, WithFishJson(
        $"Fish {dto.id} ({dto.type}) spawned during a room freeze (spawnTime {path.SpawnTime:F0} >= freeze start {RoomFreeze.StartMs:F0}); it won't be frozen", dto));

    // Already swam off (stale event or late join).
    if (path.ProgressAt(ServerClock.NowMs) >= 1.0)
    {
      GameLog.Info(LogCategory.Fish, WithFishJson($"Skip expired spawn {dto.id} ({dto.type})", dto));
      return null;
    }

    FishData template = ResolveTemplate(dto.type, dto.variant);
    if (template == null)
      return null;

    BaseFish fish = GetFishFromType(template.fishType);
    if (fish == null)
    {
      GameLog.Error(LogCategory.Fish, WithFishJson("No pooled fish for type " + template.fishType + " (" + template.variant + ")", dto));
      return null;
    }

    fish.InitializeFromServer(CreateRuntimeData(template, dto.id, (int)dto.lifespan), path, dto.hitboxRadius, dto.segments);
    Track(fish);

    if (GameLog.IsOn(LogCategory.Fish))
      GameLog.Info(LogCategory.Fish, WithFishJson(
        $"Spawn {dto.id} {dto.type} -> {template.variant} | r={fish.HitboxRadius} segments={dto.segments?.Count ?? 0} life={dto.lifespan:F0}ms " +
        $"points={dto.pathParams.points.Count} keyframes={dto.motionKeyframes?.Count ?? 0} progress={path.ProgressAt(ServerClock.NowMs):F2}", dto));
    return fish;
  }

  // The fish's own JSON, not the whole event: game:init carries every active fish.
  private static string WithFishJson(string summary, SpawnedFish dto) =>
    summary + "\n" + JsonConvert.SerializeObject(dto, Formatting.None);

  // Kills and despawns come from socket events; mock kills have no JSON.
  private static string WithEventJson(string summary) =>
    SocketIOManager.Instance != null ? SocketIOManager.Instance.WithInboundJson(summary) : summary;

  internal bool TryGetFish(string fishId, out BaseFish fish)
  {
    fish = null;
    return !string.IsNullOrEmpty(fishId) && fishById.TryGetValue(fishId, out fish);
  }

  internal void KillFish(string fishId)
  {
    if (!TryGetFish(fishId, out var fish))
    {
      GameLog.Info(LogCategory.Fish, WithEventJson($"Kill {fishId}: not on screen"));
      return;
    }

    GameLog.Info(LogCategory.Fish, WithEventJson($"Kill {fishId} ({fish.data?.variant}, state {fish.State})"));
    fish.Kill();
  }

  // game:fish_despawned. Expired fish swim off and fade; anything else is treated as killed.
  internal void RemoveFish(string fishId, string reason)
  {
    if (!TryGetFish(fishId, out var fish))
      return;

    GameLog.Info(LogCategory.Fish, WithEventJson($"Despawn {fishId} ({fish.data?.variant}) reason={reason ?? "none"} state={fish.State}"));
    if (reason == MLFGDespawnReason.Expired)
      fish.Leave();
    else
      fish.Kill();
  }

  // Own-shot KILL payout. Works whether the fish is alive, mid-death, or already released.
  internal void PlayKillPayout(string fishId, float payout)
  {
    if (TryGetFish(fishId, out var fish))
    {
      PlayStarCoinsForFish(fish);
      UIManager.Instance?.PlayRainbowWinAnimation(fish, fish.data, payout);
      fish.AddReleaseCallback(() => UIManager.Instance?.PlayCoinBlastForFish(fish));
      return;
    }

    if (!string.IsNullOrEmpty(fishId) && recentlyRemoved.TryGetValue(fishId, out var removed))
      PlayStarCoinsAtPosition(removed.data, removed.position);
  }

  // Own-shot REWARD: the fish stays alive. Coins on every paid hit, the big win only on a critical.
  internal void PlayRewardPayout(string fishId, float payout, bool isCritical)
  {
    if (!TryGetFish(fishId, out var fish))
      return;

    PlayStarCoinsAtPosition(fish.data, fish.ColliderMidPoint);
    if (fish is ImmortalFish immortal)
      immortal.TriggerWinAnimSpeedBoost();
    if (isCritical)
      UIManager.Instance?.PlayRainbowWinAnimation(fish, fish.data, payout);
  }

  // --------------------------------------------------------
  // ROOM FREEZE
  // --------------------------------------------------------

  // An unfreeze this far before EndMs belongs to an earlier freeze of an extended (double) freeze.
  private const double UnfreezeToleranceMs = 250;

  // FREEZE ack, game:room_frozen or init's activeFreeze. Repeats of the same freeze are ignored.
  internal void BeginRoomFreeze(MLFGActiveFreeze freeze, string source)
  {
    if (freeze == null || freeze.startTime <= 0 || RoomFreeze.HasSeen(freeze.startTime))
      return;

    double end = freeze.endTime > freeze.startTime ? freeze.endTime : freeze.startTime + freeze.durationMs;
    string remaining = freeze.remainingMs.HasValue
      ? $", server remainingMs {freeze.remainingMs:F0} vs ours {end - ServerClock.NowMs:F0}"
      : "";

    if (RoomFreeze.IsFrozen)
    {
      RoomFreeze.Extend(freeze.startTime, end, freeze.activatorUserId);
      GameLog.Info(LogCategory.Fish, WithEventJson(
        $"Double freeze ({source}) by {freeze.activatorUserId}: start {freeze.startTime:F0} end {end:F0}; held since {RoomFreeze.StartMs:F0}, " +
        $"now until {RoomFreeze.EndMs:F0} ({RoomFreeze.DurationMs:F0} ms){remaining}"));
    }
    else
    {
      RoomFreeze.Begin(freeze.startTime, end, freeze.activatorUserId);
      GameLog.Info(LogCategory.Fish, WithEventJson(
        $"Room frozen ({source}) by {freeze.activatorUserId}: {RoomFreeze.StartMs:F0} -> {RoomFreeze.EndMs:F0} ({RoomFreeze.DurationMs:F0} ms), " +
        $"now {ServerClock.NowMs:F0}, {activeFishes.Count} fish{remaining}"));
    }

    bool own = source == MockSource ||
      (!string.IsNullOrEmpty(freeze.activatorUserId) && freeze.activatorUserId == SocketIOManager.Instance?.UserId);
    UIManager.Instance?.OnRoomFreezeStarted(freeze.startTime, end, own);
  }

  // game:room_unfrozen (atMs = unfrozenAt), or the local fallback once EndMs has passed, whichever
  // comes first. Fish move on by the time they were held: the whole span, or less on an early end.
  internal void EndRoomFreeze(double atMs, string source)
  {
    if (!RoomFreeze.IsFrozen)
      return;

    if (atMs > 0 && atMs < RoomFreeze.EndMs - UnfreezeToleranceMs && source != MockSource)
    {
      GameLog.Info(LogCategory.Fish, WithEventJson(
        $"Unfreeze ({source}) at {atMs:F0} before the extended end {RoomFreeze.EndMs:F0}; still frozen"));
      return;
    }

    double start = RoomFreeze.StartMs;
    double shift = atMs > 0 ? Math.Min(RoomFreeze.DurationMs, Math.Max(0, atMs - start)) : RoomFreeze.DurationMs;
    int shifted = 0;
    foreach (var fish in activeFishes)
    {
      // Fish that spawned during the freeze were never held, so they don't move on.
      if (fish.Path != null && fish.Path.SpawnTime < start)
      {
        fish.Path.ShiftSpawnTime(shift);
        shifted++;
      }
    }

    RoomFreeze.Clear();
    GameLog.Info(LogCategory.Fish, WithEventJson(
      $"Room unfrozen ({source}): shifted {shifted}/{activeFishes.Count} fish by {shift:F0} ms, now {ServerClock.NowMs:F0}"));
    UIManager.Instance?.OnRoomFreezeEnded();
  }

  // A re-init replaces the fish, so there is nothing to shift.
  internal void ResetRoomFreeze()
  {
    if (!RoomFreeze.IsFrozen)
      return;
    RoomFreeze.Clear();
    UIManager.Instance?.OnRoomFreezeEnded();
  }

  // Releases every fish immediately (re-init after a reconnect).
  internal void ClearAllFish()
  {
    var fishes = activeFishes.ToArray();
    foreach (var fish in fishes)
      fish.Release();
  }

  // Called by BaseFish.Release while the fish still has its data and position.
  internal void OnFishReleased(BaseFish fish)
  {
    activeFishes.Remove(fish);
    if (GameLog.IsOn(LogCategory.Fish))
      GameLog.Info(LogCategory.Fish, $"Release {fish.Id} ({fish.data?.variant}), {activeFishes.Count} on screen");

    string id = fish.Id;
    if (!string.IsNullOrEmpty(id))
    {
      if (fishById.TryGetValue(id, out var tracked) && tracked == fish)
        fishById.Remove(id);
      RememberRemoved(id, fish);
    }

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

  private void Track(BaseFish fish)
  {
    activeFishes.Add(fish);
    if (!string.IsNullOrEmpty(fish.Id))
      fishById[fish.Id] = fish;
  }

  private void RememberRemoved(string id, BaseFish fish)
  {
    float now = Time.unscaledTime;
    while (recentlyRemovedOrder.Count > 0 && now - recentlyRemovedOrder.Peek().time > RecentlyRemovedTtl)
      recentlyRemoved.Remove(recentlyRemovedOrder.Dequeue().id);

    recentlyRemoved[id] = new RemovedFish { position = fish.ColliderMidPoint, data = fish.data };
    recentlyRemovedOrder.Enqueue((id, now));
  }

  // fishesData is the source of truth: each FishData.variant is the server's species type.
  // The server's variant field is only a fallback for older payloads.
  private FishData ResolveTemplate(string type, string variant)
  {
    FishData template = FindTemplate(type) ?? FindTemplate(variant);
    if (template == null && warnedVariants.Add(type ?? variant ?? ""))
      GameLog.Error(LogCategory.Fish, $"No FishData for server type '{type}' (variant '{variant}')");
    return template;
  }

  private FishData FindTemplate(string variant)
  {
    if (string.IsNullOrEmpty(variant))
      return null;
    return fishesData.Find(t => t != null && t.variant == variant);
  }

  private FishData CreateRuntimeData(FishData template, string fishId, int duration)
  {
    return new FishData
    {
      variant = template.variant,
      animationFrames = template.animationFrames,
      animationSpeed = template.animationSpeed,
      loop = template.loop,
      spriteSize = template.spriteSize,
      pivot = template.pivot,
      coinBlastScaleMult = template.coinBlastScaleMult,
      fishType = template.fishType,
      fishId = fishId,
      duration = duration
    };
  }

  // --------------------------------------------------------
  // EDITOR MOCK (no backend): same spawn path as the server.
  // M spawns a fish, K kills a random one with a payout.
  // --------------------------------------------------------

  internal void SpawnMockFish()
  {
    if (fishesData == null || mockFishIndex < 0 || mockFishIndex >= fishesData.Count)
    {
      GameLog.Warn(LogCategory.Fish, "mockFishIndex is out of range");
      return;
    }

    bool rightToLeft = UnityEngine.Random.value > 0.5f;
    float baseY = UnityEngine.Random.Range(200f, Playfield.Height - 200f);
    float amplitude = UnityEngine.Random.Range(40f, 140f);

    var points = new List<MLFGVec3>();
    const int pointCount = 5;
    for (int i = 0; i < pointCount; i++)
    {
      float t = i / (float)(pointCount - 1);
      float x = Mathf.Lerp(-100f, Playfield.Width + 100f, rightToLeft ? 1f - t : t);
      float y = baseY + Mathf.Sin(t * Mathf.PI * 2f) * amplitude;
      points.Add(new MLFGVec3(new Vector2(x, y)));
    }

    var dto = new SpawnedFish
    {
      id = "mock-" + (++mockCounter),
      type = fishesData[mockFishIndex].variant,
      pathType = "spline",
      pathParams = new SplinePathParams { points = points },
      spawnTime = (long)ServerClock.NowMs,
      lifespan = 12000,
      hitboxRadius = 50f
    };

    if (TryGetMockHitbox(dto.type, out MockHitbox hitbox))
    {
      dto.hitboxRadius = hitbox.hitboxRadius;
      dto.segments = hitbox.segments;
    }

    // Half the mocks pause mid-path, like the server's crab/tortoise speed profiles.
    if (UnityEngine.Random.value > 0.5f)
    {
      float total = 0f;
      for (int i = 1; i < points.Count; i++)
        total += Vector2.Distance(points[i - 1].ToVector2(), points[i].ToVector2());

      dto.motionKeyframes = new List<MotionKeyframe>
      {
        new MotionKeyframe { t = 0, d = 0 },
        new MotionKeyframe { t = 5000, d = total * 0.45 },
        new MotionKeyframe { t = 6500, d = total * 0.45 + 10 },
        new MotionKeyframe { t = 12000, d = total }
      };
    }

    SpawnFromServer(dto);
  }

  [Serializable]
  private class MockHitbox
  {
    public string type;
    public float hitboxRadius;
    public List<HitSegment> segments;
  }

  [Serializable]
  private class MockConfigRoot
  {
    public List<MockHitbox> fishes;
  }

  private bool TryGetMockHitbox(string type, out MockHitbox hitbox)
  {
    hitbox = null;
    if (mockConfig == null || string.IsNullOrEmpty(type))
      return false;

    if (mockHitboxes == null)
    {
      mockHitboxes = new Dictionary<string, MockHitbox>();
      try
      {
        var root = JsonConvert.DeserializeObject<MockConfigRoot>(mockConfig.text);
        if (root?.fishes != null)
          foreach (var f in root.fishes)
            if (f != null && !string.IsNullOrEmpty(f.type))
              mockHitboxes[f.type] = f;
      }
      catch (Exception e)
      {
        GameLog.Warn(LogCategory.Fish, "mockConfig could not be parsed: " + e.Message);
      }
    }

    return mockHitboxes.TryGetValue(type, out hitbox);
  }

  private const string MockSource = "mock";

  // Counts as our own freeze, so the button timer runs too.
  private void MockToggleFreeze()
  {
    if (RoomFreeze.IsFrozen)
    {
      EndRoomFreeze(ServerClock.NowMs, MockSource);
      return;
    }

    double now = ServerClock.NowMs;
    const double duration = 10000; // matches Config.json freeze.durationMs
    BeginRoomFreeze(new MLFGActiveFreeze { startTime = now, durationMs = duration, endTime = now + duration, activatorUserId = MockSource }, MockSource);
  }

  private void MockKillRandomFish()
  {
    var alive = activeFishes.FindAll(f => f.State == FishState.Alive);
    if (alive.Count == 0)
      return;

    BaseFish fish = alive[UnityEngine.Random.Range(0, alive.Count)];
    string id = fish.Id;
    fish.Kill();
    PlayKillPayout(id, 10f);
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

  internal void PlayStarCoinsForFish(BaseFish fish)
  {
    if (fish == null || fish.data == null)
      return;

    if (fish.data.fishType == FishType.Immortal || fish.data.fishType == FishType.Effect)
      return;

    PlayStarCoinsAtPosition(fish.data, fish.ColliderMidPoint);
  }

  internal void PlayStarCoinsAtPosition(FishData fishData, Vector3 position)
  {
    StarCoinSetPool pool = ResolveStarCoinPool(fishData);
    PlayStarCoinsFromPool(pool, position);
  }

  internal void PlayEffectFishStarCoins(BaseFish effectFish, int affectedCount)
  {
    if (effectFish == null || effectFish.data == null)
      return;

    if (effectFish.data.fishType != FishType.Effect)
      return;

    PlayStarCoinsFromPool(starCoinBigPool, effectFish.ColliderMidPoint);

    int extraCount = Mathf.Clamp(affectedCount, 0, 3);
    float radius = GetEffectStarCoinOffsetRadius(effectFish);
    for (int i = 0; i < extraCount; i++)
    {
      Vector2 offset2D = UnityEngine.Random.insideUnitCircle * radius;
      Vector3 pos = effectFish.ColliderMidPoint + new Vector3(offset2D.x, offset2D.y, 0f);
      PlayStarCoinsFromPool(starCoinBigPool, pos);
    }
  }

  internal void PlayRockCrabTorpedoStarCoins(Vector3 position)
  {
    PlayStarCoinsFromPool(starCoinBigPool, position);
  }

  private StarCoinSetPool ResolveStarCoinPool(FishData fishData)
  {
    if (fishData == null)
      return null;

    if (fishData.fishType != FishType.Normal)
      return starCoinBigPool ?? starCoinSmallPoolA ?? starCoinSmallPoolB;

    int normalIndex = GetNormalVariantIndex(fishData.variant);
    int firstTierCount = Mathf.Max(0, normalSmallPoolAThreshold);
    int secondTierCount = Mathf.Max(0, normalSmallPoolBThreshold);
    int secondTierStart = firstTierCount;
    int secondTierEnd = firstTierCount + secondTierCount;

    if (normalIndex >= 0 && normalIndex < firstTierCount)
      return starCoinSmallPoolA ?? starCoinSmallPoolB ?? starCoinBigPool;

    if (normalIndex >= secondTierStart && normalIndex < secondTierEnd)
      return starCoinSmallPoolB ?? starCoinSmallPoolA ?? starCoinBigPool;

    if (normalBigPoolThreshold > 0 && normalIndex >= normalBigPoolThreshold)
      return starCoinBigPool ?? starCoinSmallPoolB ?? starCoinSmallPoolA;

    return starCoinSmallPoolB ?? starCoinSmallPoolA ?? starCoinBigPool;
  }

  private int GetNormalVariantIndex(string variant)
  {
    if (string.IsNullOrEmpty(variant) || fishesData == null || fishesData.Count == 0)
      return -1;

    int index = 0;
    for (int i = 0; i < fishesData.Count; i++)
    {
      if (fishesData[i] == null || fishesData[i].fishType != FishType.Normal)
        continue;

      if (fishesData[i].variant == variant)
        return index;

      index++;
    }

    return -1;
  }

  private void PlayStarCoinsFromPool(StarCoinSetPool pool, Vector3 position)
  {
    if (starCoinDelay > 0f)
    {
      StartCoroutine(PlayStarCoinsAfterDelay(pool, position, starCoinDelay));
      return;
    }

    PlayStarCoinsFromPoolImmediate(pool, position);
  }

  private IEnumerator PlayStarCoinsAfterDelay(StarCoinSetPool pool, Vector3 position, float delay)
  {
    yield return new WaitForSeconds(delay);
    PlayStarCoinsFromPoolImmediate(pool, position);
  }

  private void PlayStarCoinsFromPoolImmediate(StarCoinSetPool pool, Vector3 position)
  {
    if (!TryEnsureStarCoinTargets())
      return;

    if (pool == null)
      return;

    var view = pool.GetFromPool();
    if (view == null)
      return;

    view.Play(
      position,
      starCoinFirstTarget.position,
      starCoinTarget.position,
      v => pool.ReturnToPool(v)
    );
  }

  private bool TryEnsureStarCoinTargets()
  {
    if (starCoinTarget != null && starCoinFirstTarget != null)
      return true;

    if (!starCoinTargetMissingLogged)
    {
      GameLog.Warn(LogCategory.UI, "Star coin targets not assigned");
      starCoinTargetMissingLogged = true;
    }
    return false;
  }

  private float GetEffectStarCoinOffsetRadius(BaseFish fish)
  {
    float radius = Mathf.Max(0f, effectStarCoinOffsetRadius);
    if (fish == null || fish.data == null)
      return Mathf.Min(radius, effectStarCoinOffsetRadiusMax);

    if (fish.Rect != null)
    {
      float width = fish.Rect.rect.width;
      radius += width * Mathf.Max(0f, effectStarCoinOffsetRadiusPerFishWidth);
    }

    return Mathf.Min(radius, effectStarCoinOffsetRadiusMax);
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

  // --------------------------------------------------------
  // OLD BACKEND (paused): spawns without a server path (fish do not move).
  // --------------------------------------------------------

  internal FishData ToFishData(Fish backendFish)
  {
    var baseData = FindTemplate(backendFish.variant);
    return baseData != null ? CreateRuntimeData(baseData, backendFish.id, backendFish.lifespan) : null;
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

  internal BaseFish SpawnFishFromBackend(FishData data)
  {
    BaseFish fish = GetFishFromType(data.fishType);

    if (fish == null)
    {
      GameLog.Error(LogCategory.Legacy, "Fish Not Found! " + data.variant);
      return null;
    }

    fish.Initialize(data);

    Track(fish);
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
  // Normalized RectTransform pivot. The path point (and the server hit circle) sits on it, so
  // shift it to center the art on the hitbox. Hitboxes themselves come from the server.
  public Vector2 pivot = new Vector2(0.5f, 0.5f);
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
