using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class EffectFish : BaseFish
{
  [SerializeField, Range(0f, 0.49f)] private float crabEdgePercentMin = 0.25f;
  [SerializeField, Range(0f, 0.49f)] private float crabEdgePercentMax = 0.4f;
  [SerializeField] private BubbleWhirlpoolController bubbleWhirlpool;
  [Header("Effect Fish Shared")]
  [SerializeField] private Vector3 blueBlastLocalOffset = Vector3.zero;
  [SerializeField] private float blueBlastScaleMultiplier = 1f;
  [SerializeField] private float blueBlastMaxDuration = 1.5f;
  [SerializeField] private float effectScaleMultiplier = 1f;
  [Header("Bubble Crab Death")]
  [SerializeField] private float bubbleCrabShakeRadius = 12f;
  [SerializeField] private float bubbleCrabShakeSpeed = 45f;
  [SerializeField] private float bubbleCrabSpawnRadiusMultiplier = 2.2f;
  [SerializeField] private float bubbleCrabRadialSpeedLerpDuration = 0.2f;
  [SerializeField] private float bubbleCraAngularSpeedLerpDuration = 0.4f;
  [SerializeField] private float bubbleCrabSpawnIntervalMultiplier = 0.5f;
  [SerializeField] private float bubbleCrabRadialSpeedMultiplier = 1.2f;
  [SerializeField] private float bubbleCrabAngularSpeedMultiplier = 7f;
  [SerializeField] private float affectedFishTravelDuration = 1.2f;
  [SerializeField] private float affectedFishRadiusOffset = 6f;
  [SerializeField] private float affectedFishPulseDuration = 0.7f;
  [SerializeField] private float affectedFishPulseScale = 0.08f;
  [SerializeField] private float affectedFishPulseScaleSpeed = 8f;
  [SerializeField] private float affectedFishAnimationSpeedMultiplier = 2f;
  [SerializeField] private float affectedFishAngularSpeed = 720f;
  [SerializeField] private float affectedFishAngularSpeedMinMultiplier = 0.35f;
  [SerializeField] private float affectedFishAngularSpeedMaxMultiplier = 1.2f;
  [SerializeField] private float affectedFishSelfSpinSpeed = 360f;
  [SerializeField] private float bubbleCrabTorpedoImpactTimeout = 2f;
  [Header("Blue Fish Death")]
  [SerializeField] private float blueFishShakeRadius = 10f;
  [SerializeField] private float blueFishShakeSpeed = 45f;
  [SerializeField] private float blueFishAnimationSpeedMultiplier = 2f;
  [SerializeField] private float blueFishAttractDuration = 1.2f;
  [SerializeField] private float blueFishAffectedSpinSpeed = 360f;
  [SerializeField] private float electricLinkHeight = 200f;
  [SerializeField] private float electricLinkDuration = 0.35f;
  [SerializeField] private float electricChainDelay = 0.1f;
  [SerializeField] private float electricBallStartScaleMultiplier = 1.4f;
  [SerializeField] private float electricBallTargetSizeMultiplier = 1.5f;
  [SerializeField] private float electricBallScaleDownDuration = 0.25f;
  [SerializeField] private float blueFishChainExtraDelayBuffer = 0.15f;
  private const float MinSpeedMultiplier = 0.6f;
  private const float MaxSpeedMultiplier = 1.6f;
  private Coroutine segmentedSpeedRoutine;
  private Coroutine bubbleCrabShakeRoutine;
  private Coroutine bubbleCrabEffectRoutine;
  private Coroutine blueFishShakeRoutine;
  private Coroutine blueFishEffectRoutine;
  private Coroutine blueBlastRoutine;
  private Vector3 bubbleCrabBaseLocalPos;
  private Vector3 blueFishBaseLocalPos;
  private bool bubbleCrabDeathActive;
  private bool bubbleCrabAwaitingTorpedoImpact;
  private bool bubbleCrabTorpedoImpactReceived;
  private const string BubbleCrabVariant = "effect_bubblecrab_fish";
  private const string BlueFishVariant = "effect_blue_fish";
  private bool blueFishDeathActive;
  private enum EffectKind
  {
    BlueBlast,
    ElectricBall
  }

  private struct PooledEffect
  {
    internal ImageAnimation anim;
    internal bool pooled;
    internal EffectKind kind;
  }
  private struct PooledLink
  {
    internal ElectricLinkEffectView view;
    internal bool pooled;
  }
  private readonly Dictionary<BaseFish, PooledEffect> blueFishBallByTarget = new Dictionary<BaseFish, PooledEffect>();
  private readonly List<PooledEffect> blueFishTransientEffects = new List<PooledEffect>();
  private readonly List<PooledLink> blueFishLinkEffects = new List<PooledLink>();
  private readonly Dictionary<BaseFish, Coroutine> blueFishMoveRoutines = new Dictionary<BaseFish, Coroutine>();
  private readonly Dictionary<BaseFish, Coroutine> blueFishSpinRoutines = new Dictionary<BaseFish, Coroutine>();
  private int blueFishMovingCount;
  private float blueFishMoveStartTime;
  private bool blueFishMoveWindowStarted;
  private float blueFishMoveArrivalTime;

  internal override void Initialize(FishData data)
  {
    base.Initialize(data);
    SetupFallbackMovement();
    StartRockCrabSpeedVariation();
    StartBubbleCrabWhirlpool();
  }

  internal override void ResetFish()
  {
    bubbleCrabDeathActive = false;
    bubbleCrabAwaitingTorpedoImpact = false;
    bubbleCrabTorpedoImpactReceived = false;
    if (bubbleCrabShakeRoutine != null)
    {
      StopCoroutine(bubbleCrabShakeRoutine);
      bubbleCrabShakeRoutine = null;
    }
    if (bubbleCrabEffectRoutine != null)
    {
      StopCoroutine(bubbleCrabEffectRoutine);
      bubbleCrabEffectRoutine = null;
    }
    ResetBlueFishEffects();
    if (blueBlastRoutine != null)
    {
      StopCoroutine(blueBlastRoutine);
      blueBlastRoutine = null;
    }
    if (bubbleWhirlpool != null)
    {
      bubbleWhirlpool.SetSpawnRadiusMultiplier(1f);
      bubbleWhirlpool.ResetSpawnInterval();
      bubbleWhirlpool.ResetRadialSpeed();
      bubbleWhirlpool.ResetAngularSpeed();
    }
    bubbleWhirlpool?.StopAndClear();
    if (segmentedSpeedRoutine != null)
    {
      StopCoroutine(segmentedSpeedRoutine);
      segmentedSpeedRoutine = null;
    }
    base.ResetFish();
  }

  private void StartRockCrabSpeedVariation()
  {
    if (data == null || data.variant != "effect_rockcrab_fish")
      return;

    if (segmentedSpeedRoutine != null)
      StopCoroutine(segmentedSpeedRoutine);

    segmentedSpeedRoutine = StartCoroutine(CrabSegmentedSpeedRoutine());
  }

  private void StartBubbleCrabWhirlpool()
  {
    if (data == null || data.variant != BubbleCrabVariant)
      return;

    if (bubbleWhirlpool == null)
      bubbleWhirlpool = GetComponent<BubbleWhirlpoolController>();

    if (bubbleWhirlpool == null)
      return;

    bubbleWhirlpool.Begin();
  }

  internal void TriggerBubbleCrabDeath(List<BaseFish> affectedFishes)
  {
    if (data == null || data.variant != BubbleCrabVariant)
      return;

    if (bubbleCrabDeathActive)
      return;

    bubbleCrabDeathActive = true;
    bubbleCrabAwaitingTorpedoImpact = false;
    bubbleCrabTorpedoImpactReceived = false;
    MarkPendingDeath();
    StopPathMovement();
    FishManager.Instance?.MoveToAnimParent(this);

    if (bubbleWhirlpool == null)
      bubbleWhirlpool = GetComponent<BubbleWhirlpoolController>();

    if (bubbleWhirlpool != null)
    {
      bubbleWhirlpool.StopAndClear();
      bubbleWhirlpool.SetSpawnRadiusMultiplier(bubbleCrabSpawnRadiusMultiplier);
      bubbleWhirlpool.SetSpawnIntervalMultiplier(bubbleCrabSpawnIntervalMultiplier);
      bubbleWhirlpool.LerpRadialSpeedMultiplier(
        bubbleCrabRadialSpeedMultiplier,
        bubbleCrabRadialSpeedLerpDuration
      );
      bubbleWhirlpool.LerpAngularSpeedMultiplier(
        bubbleCrabAngularSpeedMultiplier,
        bubbleCraAngularSpeedLerpDuration
      );
      bubbleWhirlpool.Begin();
    }

    bubbleCrabBaseLocalPos = transform.localPosition;
    if (blueBlastRoutine != null)
      StopCoroutine(blueBlastRoutine);
    blueBlastRoutine = StartCoroutine(PlayBlueBlastEffectRoutine(null));
    if (bubbleCrabShakeRoutine != null)
      StopCoroutine(bubbleCrabShakeRoutine);
    bubbleCrabShakeRoutine = StartCoroutine(BubbleCrabShakeRoutine());

    if (bubbleCrabEffectRoutine != null)
      StopCoroutine(bubbleCrabEffectRoutine);
    bubbleCrabEffectRoutine = StartCoroutine(BubbleCrabEffectRoutine(affectedFishes));
  }

  internal override void OnTorpedoImpact() { }

  private IEnumerator BubbleCrabShakeRoutine()
  {
    while (bubbleCrabDeathActive)
    {
      float x = Mathf.PerlinNoise(Time.time * bubbleCrabShakeSpeed, 0f) * 2f - 1f;
      float y = Mathf.PerlinNoise(0f, Time.time * bubbleCrabShakeSpeed) * 2f - 1f;
      Vector3 offset = new Vector3(x, y, 0f) * bubbleCrabShakeRadius;
      transform.localPosition = bubbleCrabBaseLocalPos + offset;
      yield return null;
    }

    transform.localPosition = bubbleCrabBaseLocalPos;
  }

  private IEnumerator BubbleCrabEffectRoutine(List<BaseFish> affectedFishes)
  {
    if (bubbleCrabAwaitingTorpedoImpact)
    {
      float timer = 0f;
      float timeout = Mathf.Max(0f, bubbleCrabTorpedoImpactTimeout);
      while (!bubbleCrabTorpedoImpactReceived && timer < timeout)
      {
        timer += Time.deltaTime;
        yield return null;
      }
    }

    SetAnimationSpeedMultiplier(3f);
    List<BaseFish> validTargets = CollectAffectedTargets(affectedFishes);

    int remaining = validTargets.Count;
    if (remaining > 0)
    {
      foreach (var fish in validTargets)
        StartCoroutine(OrbitAffectedFishAroundCrab(fish, () => remaining--));

      yield return new WaitUntil(() => remaining <= 0);
    }

    PlayCoinBlast(this);
    
    foreach (var fish in validTargets)
    {
      fish.PendingVisualDeath = false;
      fish.ForceDespawn();
    }
    
    bubbleCrabDeathActive = false;
    PendingVisualDeath = false;
    ForceDespawn();
  }

  internal void TriggerBlueFishDeath(List<BaseFish> affectedFishes)
  {
    if (data == null || data.variant != BlueFishVariant)
      return;

    if (blueFishDeathActive)
      return;

    blueFishDeathActive = true;
    MarkPendingDeath();
    StopPathMovement();
    FishManager.Instance?.MoveToAnimParent(this);

    blueFishBaseLocalPos = transform.localPosition;

    if (blueFishEffectRoutine != null)
      StopCoroutine(blueFishEffectRoutine);
    blueFishEffectRoutine = StartCoroutine(BlueFishEffectRoutine(affectedFishes));
  }

  private void CacheAndReparentAffectedFish(BaseFish fish)
  {
    if (fish == null)
      return;

    FishManager.Instance?.MoveToAnimParent(fish);
  }

  private List<BaseFish> CollectAffectedTargets(List<BaseFish> affectedFishes)
  {
    List<BaseFish> validTargets = new List<BaseFish>();
    if (affectedFishes == null)
      return validTargets;

    foreach (var fish in affectedFishes)
    {
      if (fish == null || fish == this)
        continue;

      fish.MarkPendingDeath();
      fish.SetAlpha(1f);
      fish.StopPathMovement();
      CacheAndReparentAffectedFish(fish);
      validTargets.Add(fish);
    }

    return validTargets;
  }

  private IEnumerator OrbitAffectedFishAroundCrab(BaseFish fish, System.Action onComplete)
  {
    if (fish == null)
    {
      onComplete?.Invoke();
      yield break;
    }

    Vector3 crabPos = transform.position;
    Vector3 startPos = fish.transform.position;
    Vector3 offset = startPos - crabPos;
    float startRadius = Mathf.Max(0.01f, offset.magnitude);
    float targetRadius = startRadius + affectedFishRadiusOffset;
    float angle = Mathf.Atan2(offset.y, offset.x);
    int angularSign = bubbleWhirlpool != null
      ? bubbleWhirlpool.GetAngularDirectionSign()
      : 1;
    int scaleSign = 1;
    if (bubbleWhirlpool != null)
    {
      Vector3 lossyScale = bubbleWhirlpool.transform.lossyScale;
      float xSign = Mathf.Sign(lossyScale.x);
      float ySign = Mathf.Sign(lossyScale.y);
      if (xSign == 0f) xSign = 1f;
      if (ySign == 0f) ySign = 1f;
      scaleSign = xSign * ySign < 0f ? -1 : 1;
    }
    float angularSpeedRad = affectedFishAngularSpeed * angularSign * scaleSign * Mathf.Deg2Rad;
    Vector3 baseScale = fish.transform.localScale;
    fish.SetAnimationSpeedMultiplier(affectedFishAnimationSpeedMultiplier);
    float timer = 0f;
    float duration = Mathf.Max(0.01f, affectedFishTravelDuration);

    while (timer < duration && fish != null)
    {
      timer += Time.deltaTime;
      crabPos = transform.position;
      float t = Mathf.Clamp01(timer / duration);
      float radius = Mathf.Lerp(startRadius, targetRadius, t);
      float angularSpeedScale = Mathf.Lerp(
        affectedFishAngularSpeedMinMultiplier,
        affectedFishAngularSpeedMaxMultiplier,
        t
      );
      angle += angularSpeedRad * angularSpeedScale * Time.deltaTime;

      Vector3 pos = crabPos + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
      fish.transform.position = pos;
      fish.transform.Rotate(0f, 0f, affectedFishSelfSpinSpeed * Time.deltaTime);

      if (timer <= affectedFishPulseDuration)
      {
        float pulse01 = (Mathf.Sin(timer * affectedFishPulseScaleSpeed - (Mathf.PI * 0.5f)) + 1f) * 0.5f;
        float scalePulse = 1f + pulse01 * affectedFishPulseScale;
        fish.transform.localScale = baseScale * scalePulse;
      }
      else
      {
        fish.transform.localScale = baseScale;
      }

      yield return null;
    }

    if (fish != null)
    {
      fish.transform.localScale = baseScale;
      fish.SetAnimationSpeedMultiplier(1f);
    }

    onComplete?.Invoke();
  }

  private IEnumerator BlueFishEffectRoutine(List<BaseFish> affectedFishes)
  {
    List<BaseFish> validTargets = CollectAffectedTargets(affectedFishes);

    if (blueBlastRoutine != null)
      StopCoroutine(blueBlastRoutine);
    blueBlastRoutine = StartCoroutine(PlayBlueBlastEffectRoutine(() =>
    {
      StopBlueFishShake();
    }));
    StartBlueFishShake();
    yield return blueBlastRoutine;
    blueBlastRoutine = null;

    SetAnimationSpeedMultiplier(blueFishAnimationSpeedMultiplier);

    if (validTargets.Count > 0)
      yield return StartCoroutine(ChainLightningRoutine(validTargets));

    PlayCoinBlast(this);

    foreach (var fish in validTargets)
    {
      if (fish == null)
        continue;
      fish.PendingVisualDeath = false;
      fish.ForceDespawn();
    }

    blueFishDeathActive = false;
    PendingVisualDeath = false;
    ForceDespawn();
  }

  private IEnumerator ChainLightningRoutine(List<BaseFish> targets)
  {
    blueFishMovingCount = 0;
    blueFishMoveStartTime = 0f;
    blueFishMoveWindowStarted = false;
    blueFishMoveArrivalTime = 0f;

    float chainStepDuration = electricLinkDuration + Mathf.Max(0f, electricChainDelay);
    float chainTime = targets.Count * chainStepDuration;
    blueFishMoveArrivalTime = Time.time + blueFishAttractDuration + chainTime + blueFishChainExtraDelayBuffer;

    BaseFish source = this;
    for (int i = 0; i < targets.Count; i++)
    {
      BaseFish target = targets[i];
      if (target == null)
        continue;

      SpawnElectricBall(target);
      yield return StartCoroutine(PlayElectricLinkRoutine(source, target));
      StartMovingAffectedFish(source);

      source = target;

      if (electricChainDelay > 0f)
        yield return new WaitForSeconds(electricChainDelay);
    }

    StartMovingAffectedFish(source);

    if (blueFishMovingCount > 0)
      yield return new WaitUntil(() => blueFishMovingCount <= 0);
  }

  private void StartBlueFishShake()
  {
    if (blueFishShakeRoutine != null)
      StopCoroutine(blueFishShakeRoutine);
    blueFishShakeRoutine = StartCoroutine(BlueFishShakeRoutine());
  }

  private void StopBlueFishShake()
  {
    if (blueFishShakeRoutine != null)
    {
      StopCoroutine(blueFishShakeRoutine);
      blueFishShakeRoutine = null;
    }
    transform.localPosition = blueFishBaseLocalPos;
  }

  private IEnumerator BlueFishShakeRoutine()
  {
    while (blueFishDeathActive)
    {
      float x = Mathf.PerlinNoise(Time.time * blueFishShakeSpeed, 0f) * 2f - 1f;
      float y = Mathf.PerlinNoise(0f, Time.time * blueFishShakeSpeed) * 2f - 1f;
      Vector3 offset = new Vector3(x, y, 0f) * blueFishShakeRadius;
      transform.localPosition = blueFishBaseLocalPos + offset;
      yield return null;
    }

    transform.localPosition = blueFishBaseLocalPos;
  }

  private IEnumerator PlayBlueBlastEffectRoutine(System.Action onComplete)
  {
    if (BlueBlastEffectPool.Instance == null)
    {
      onComplete?.Invoke();
      yield break;
    }

    bool pooled;
    ImageAnimation effect = GetBlueBlastEffect(out pooled);
    if (effect == null)
    {
      onComplete?.Invoke();
      yield break;
    }

    effect.transform.SetParent(transform, false);
    effect.transform.localPosition = blueBlastLocalOffset;
    effect.transform.localScale = Vector3.one * (blueBlastScaleMultiplier * effectScaleMultiplier);
    blueFishTransientEffects.Add(new PooledEffect
    {
      anim = effect,
      pooled = pooled,
      kind = EffectKind.BlueBlast
    });

    bool finished = false;
    System.Action handleComplete = () => finished = true;
    effect.OnAnimationComplete = null;
    effect.OnAnimationComplete += handleComplete;
    effect.StartAnimation();

    float timer = 0f;
    float timeout = Mathf.Max(0.01f, blueBlastMaxDuration);
    while (!finished && timer < timeout)
    {
      timer += Time.deltaTime;
      yield return null;
    }

    effect.OnAnimationComplete -= handleComplete;
    ReturnBlueBlastEffect(effect, pooled);
    blueFishTransientEffects.RemoveAll(entry => entry.anim == effect);

    onComplete?.Invoke();
  }

  private IEnumerator PlayElectricLinkRoutine(BaseFish from, BaseFish to)
  {
    if (ElectricLinkEffectPool.Instance == null || from == null || to == null)
      yield break;

    bool pooled;
    ElectricLinkEffectView linkView = GetElectricLinkEffect(out pooled);
    if (linkView == null)
      yield break;

    Transform parent = FishManager.Instance != null && FishManager.Instance.AnimParent != null
      ? FishManager.Instance.AnimParent
      : (transform.parent != null ? transform.parent : transform);
    linkView.transform.SetParent(parent, false);
    linkView.transform.SetAsLastSibling();
    linkView.StartAnimation();
    blueFishLinkEffects.Add(new PooledLink { view = linkView, pooled = pooled });

    float timer = 0f;
    float duration = Mathf.Max(0.01f, electricLinkDuration);
    while (timer < duration && from != null && to != null)
    {
      timer += Time.deltaTime;
      Vector3 start = from.ColliderMidPoint;
      Vector3 end = to.ColliderMidPoint;
      linkView.UpdateLink(start, end, electricLinkHeight);

      yield return null;
    }

    ReturnElectricLinkEffect(linkView, pooled);
    blueFishLinkEffects.RemoveAll(entry => entry.view == linkView);
  }

  private void SpawnElectricBall(BaseFish target)
  {
    if (ElectricBallEffectPool.Instance == null || target == null)
      return;

    if (blueFishBallByTarget.ContainsKey(target))
      return;

    bool pooled;
    ImageAnimation anim = GetElectricBallEffect(out pooled);
    if (anim == null)
      return;

    anim.transform.SetParent(target.transform, false);
    RectTransform ballRect = anim.GetComponent<RectTransform>();
    if (ballRect != null)
    {
      ballRect.localPosition = Vector3.zero;
      ballRect.localRotation = Quaternion.identity;

      Vector3 targetScale = GetElectricBallTargetScale(target, ballRect);
      Vector3 startScale = targetScale * Mathf.Max(0.01f, electricBallStartScaleMultiplier);
      ballRect.localScale = startScale;
      StartCoroutine(ScaleElectricBallRoutine(ballRect, startScale, targetScale));
    }

    anim.StartAnimation();
    blueFishBallByTarget[target] = new PooledEffect
    {
      anim = anim,
      pooled = pooled,
      kind = EffectKind.ElectricBall
    };

    StartSpinForTarget(target);
  }

  private Vector3 GetElectricBallTargetScale(BaseFish target, RectTransform ballRect)
  {
    Vector2 fishSize = target.Rect != null ? target.Rect.sizeDelta : Vector2.one * 100f;
    Vector2 ballSize = ballRect.sizeDelta;
    if (ballSize.x <= 0f || ballSize.y <= 0f)
      ballSize = Vector2.one * 100f;

    float targetWidth = fishSize.x * Mathf.Max(0.01f, electricBallTargetSizeMultiplier);
    float scale = targetWidth / ballSize.x;
    return Vector3.one * Mathf.Max(0.01f, scale);
  }

  private ImageAnimation GetBlueBlastEffect(out bool pooled)
  {
    var pool = BlueBlastEffectPool.Instance;
    if (pool != null)
    {
      pooled = true;
      return pool.GetFromPool();
    }

    pooled = false;
    return null;
  }

  private void ReturnBlueBlastEffect(ImageAnimation effect, bool pooled)
  {
    if (effect == null)
      return;

    if (pooled && BlueBlastEffectPool.Instance != null)
    {
      effect.StopAnimation();
      BlueBlastEffectPool.Instance.ReturnToPool(effect);
      return;
    }

    Destroy(effect.gameObject);
  }

  private ElectricLinkEffectView GetElectricLinkEffect(out bool pooled)
  {
    var pool = ElectricLinkEffectPool.Instance;
    if (pool != null)
    {
      pooled = true;
      return pool.GetFromPool();
    }

    pooled = false;
    return null;
  }

  private void ReturnElectricLinkEffect(ElectricLinkEffectView effect, bool pooled)
  {
    if (effect == null)
      return;

    if (pooled && ElectricLinkEffectPool.Instance != null)
    {
      effect.StopAnimation();
      ElectricLinkEffectPool.Instance.ReturnToPool(effect);
      return;
    }

    Destroy(effect.gameObject);
  }

  private ImageAnimation GetElectricBallEffect(out bool pooled)
  {
    var pool = ElectricBallEffectPool.Instance;
    if (pool != null)
    {
      pooled = true;
      return pool.GetFromPool();
    }

    pooled = false;
    return null;
  }

  private void ReturnElectricBallEffect(ImageAnimation effect, bool pooled)
  {
    if (effect == null)
      return;

    if (pooled && ElectricBallEffectPool.Instance != null)
    {
      effect.StopAnimation();
      ElectricBallEffectPool.Instance.ReturnToPool(effect);
      return;
    }

    Destroy(effect.gameObject);
  }

  private IEnumerator ScaleElectricBallRoutine(RectTransform ballRect, Vector3 start, Vector3 target)
  {
    float timer = 0f;
    float duration = Mathf.Max(0.01f, electricBallScaleDownDuration);
    while (timer < duration && ballRect != null)
    {
      timer += Time.deltaTime;
      float t = Mathf.Clamp01(timer / duration);
      ballRect.localScale = Vector3.Lerp(start, target, t);
      yield return null;
    }

    if (ballRect != null)
      ballRect.localScale = target;
  }

  private void StartSpinForTarget(BaseFish target)
  {
    if (target == null || blueFishSpinRoutines.ContainsKey(target))
      return;

    Coroutine routine = StartCoroutine(SpinAffectedFishRoutine(target));
    blueFishSpinRoutines[target] = routine;
  }

  private void StopSpinForTarget(BaseFish target)
  {
    if (target == null)
      return;

    if (blueFishSpinRoutines.TryGetValue(target, out var routine) && routine != null)
      StopCoroutine(routine);
    blueFishSpinRoutines.Remove(target);
  }

  private IEnumerator SpinAffectedFishRoutine(BaseFish target)
  {
    while (blueFishDeathActive && target != null)
    {
      target.transform.Rotate(0f, 0f, blueFishAffectedSpinSpeed * Time.deltaTime);
      yield return null;
    }
    if (target != null)
      blueFishSpinRoutines.Remove(target);
  }

  private void StartMovingAffectedFish(BaseFish fish)
  {
    if (fish == null || fish == this)
      return;

    if (blueFishMoveRoutines.ContainsKey(fish))
      return;

    if (!blueFishMoveWindowStarted)
    {
      blueFishMoveWindowStarted = true;
      blueFishMoveStartTime = Time.time;
      if (blueFishMoveArrivalTime <= 0f)
        blueFishMoveArrivalTime = blueFishMoveStartTime + blueFishAttractDuration;
    }

    float remaining = blueFishMoveArrivalTime - Time.time;
    float duration = Mathf.Max(0.1f, remaining);
    Coroutine routine = StartCoroutine(MoveAffectedFishToBlueRoutine(fish, duration));
    blueFishMoveRoutines[fish] = routine;
    blueFishMovingCount++;
  }

  private IEnumerator MoveAffectedFishToBlueRoutine(BaseFish fish, float duration)
  {
    Vector3 startPos = fish.transform.position;
    Vector3 targetPos = transform.position;
    float timer = 0f;

    while (timer < duration && fish != null)
    {
      timer += Time.deltaTime;
      float t = Mathf.Clamp01(timer / duration);
      fish.transform.position = Vector3.Lerp(startPos, targetPos, t);
      yield return null;
    }

    if (fish != null)
      fish.transform.position = targetPos;

    StopSpinForTarget(fish);

    blueFishMoveRoutines.Remove(fish);
    blueFishMovingCount = Mathf.Max(0, blueFishMovingCount - 1);
  }

  private void ResetBlueFishEffects()
  {
    blueFishDeathActive = false;

    if (blueFishShakeRoutine != null)
    {
      StopCoroutine(blueFishShakeRoutine);
      blueFishShakeRoutine = null;
    }
    if (blueFishEffectRoutine != null)
    {
      StopCoroutine(blueFishEffectRoutine);
      blueFishEffectRoutine = null;
    }

    foreach (var pair in blueFishMoveRoutines)
    {
      if (pair.Value != null)
        StopCoroutine(pair.Value);
    }
    blueFishMoveRoutines.Clear();

    foreach (var pair in blueFishSpinRoutines)
    {
      if (pair.Value != null)
        StopCoroutine(pair.Value);
    }
    blueFishSpinRoutines.Clear();

    foreach (var pair in blueFishBallByTarget)
    {
      ReturnElectricBallEffect(pair.Value.anim, pair.Value.pooled);
    }
    blueFishBallByTarget.Clear();
    blueFishMovingCount = 0;
    blueFishMoveStartTime = 0f;
    blueFishMoveWindowStarted = false;
    blueFishMoveArrivalTime = 0f;

    for (int i = blueFishTransientEffects.Count - 1; i >= 0; i--)
    {
      var entry = blueFishTransientEffects[i];
      if (entry.kind == EffectKind.BlueBlast)
        ReturnBlueBlastEffect(entry.anim, entry.pooled);
    }
    blueFishTransientEffects.Clear();

    for (int i = blueFishLinkEffects.Count - 1; i >= 0; i--)
    {
      var entry = blueFishLinkEffects[i];
      ReturnElectricLinkEffect(entry.view, entry.pooled);
    }
    blueFishLinkEffects.Clear();
  }

  private void PlayCoinBlast(BaseFish fish)
  {
    if (fish == null || fish.data == null)
    {
      Debug.LogError("Fish Data not found");
      return;
    }

    var coinAnimation = CoinBlastAnimPool.Instance.GetFromPool();
    if (coinAnimation == null)
    {
      Debug.LogError("coinAnimation not found"); 
      return;
    }

    Vector3 pos = fish.ColliderMidPoint;
    coinAnimation.transform.SetPositionAndRotation(pos, Quaternion.identity);
    coinAnimation.transform.localScale = Vector3.one * effectScaleMultiplier;
  }

  private IEnumerator CrabSegmentedSpeedRoutine()
  {
    yield return null;

    if (splineController == null || splineController.Spline == null)
      yield break;

    float pathLength = splineController.Length;
    if (pathLength <= Mathf.Epsilon)
      yield break;

    float minEdge = Mathf.Min(crabEdgePercentMin, crabEdgePercentMax);
    float maxEdge = Mathf.Max(crabEdgePercentMin, crabEdgePercentMax);
    float edgePercent = Random.Range(minEdge, maxEdge);
    edgePercent = Mathf.Clamp(edgePercent, 0f, 0.49f);
    float middlePercent = 1f - (edgePercent * 2f);
    if (middlePercent <= Mathf.Epsilon)
      yield break;

    float[] segmentDistances = new float[3]
    {
      edgePercent * pathLength,
      middlePercent * pathLength,
      edgePercent * pathLength
    };

    float speed1 = Random.Range(MinSpeedMultiplier, MaxSpeedMultiplier);
    float speed2 = speed1 < 1f
      ? Random.Range(1f, MaxSpeedMultiplier)
      : Random.Range(MinSpeedMultiplier, 1f);
    float speed3 = speed2 < 1f
      ? Random.Range(1f, MaxSpeedMultiplier)
      : Random.Range(MinSpeedMultiplier, 1f);

    float[] segmentSpeeds = new float[3] { speed1, speed2, speed3 };

    float weightedTimeSum = 0f;
    for (int i = 0; i < segmentDistances.Length; i++)
      weightedTimeSum += segmentDistances[i] / segmentSpeeds[i];

    float scaleFactor = weightedTimeSum > Mathf.Epsilon
      ? weightedTimeSum / pathLength
      : 1f;

    float targetPos = splineController.Position;
    for (int i = 0; i < segmentDistances.Length; i++)
    {
      if (splineController == null)
        break;

      targetPos = Mathf.Min(targetPos + segmentDistances[i], splineController.Length);
      SetSpeedMultiplier(segmentSpeeds[i] * scaleFactor);

      yield return new WaitUntil(() =>
        splineController == null ||
        splineController.Position >= targetPos ||
        splineController.Position >= splineController.Length
      );
    }

    SetSpeedMultiplier(1f);
  }
}
