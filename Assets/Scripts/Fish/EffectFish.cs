using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

internal class EffectFish : BaseFish
{
  [SerializeField, Range(0f, 0.49f)] private float crabEdgePercentMin = 0.25f;
  [SerializeField, Range(0f, 0.49f)] private float crabEdgePercentMax = 0.4f;
  [SerializeField] private BubbleWhirlpoolController bubbleWhirlpool;
  [Header("Effect Fish Shared")]
  [SerializeField] private Vector3 blueBlastLocalOffset = Vector3.zero;
  [SerializeField] private float blueBlastScaleMultiplier = 1f;
  [SerializeField] private float effectScaleMultiplier = 1f;
  [SerializeField] private float effectFishTorpedoWaitTimeout = 2f;
  [Header("Bubble Crab Death")]
  [SerializeField] private float bubbleCrabShakeRadius = 12f;
  [SerializeField] private float bubbleCrabShakeSpeed = 45f;
  [SerializeField] private Image bubbleCrabWhirlpoolImage;
  [SerializeField] private float bubbleCrabWhirlpoolFadeInDuration = 0.2f;
  [SerializeField] private float bubbleCrabWhirlpoolRotateDuration = 1.2f;
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
  [SerializeField] private GameObject rockCrabTorpedoVisual;
  [Header("Rock Crab Death")]
  [SerializeField] private float rockCrabShakeRadius = 10f;
  [SerializeField] private float rockCrabShakeSpeed = 40f;
  [SerializeField] private float rockCrabPreFireDelay = 0.4f;
  [SerializeField] private float rockCrabTorpedoInterval = 0.08f;
  [SerializeField] private float rockCrabTorpedoHitTimeout = 2f;
  [SerializeField] private float rockCrabEscapeSpeedMultiplier = 8f;
  [SerializeField] private float rockCrabEscapeScaleMultiplier = 1.15f;
  [SerializeField] private float rockCrabEscapeScaleDuration = 0.2f;
  [Header("Rock Crab Torpedo Visual")]
  [SerializeField] private float rockCrabTorpedoRockAngle = 6f;
  [SerializeField] private float rockCrabTorpedoRockDuration = 0.16f;
  [Header("Blue Fish Death")]
  [SerializeField] private float blueFishShakeRadius = 10f;
  [SerializeField] private float blueFishShakeSpeed = 45f;
  [SerializeField] private float blueFishAnimationSpeedMultiplier = 2f;
  [SerializeField] private float blueFishExplosionDistance = 60f;
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
  private Coroutine rockCrabShakeRoutine;
  private Coroutine rockCrabEffectRoutine;
  private Tween rockCrabTorpedoRockTween;
  private Tween bubbleCrabWhirlpoolRotateTween;
  private Tween bubbleCrabWhirlpoolFadeTween;
  private Vector3 bubbleCrabBaseLocalPos;
  private Vector3 blueFishBaseLocalPos;
  private Vector3 rockCrabBaseLocalPos;
  private bool bubbleCrabDeathActive;
  private bool bubbleCrabAwaitingTorpedoImpact;
  private bool bubbleCrabTorpedoImpactReceived;
  private bool rockCrabDeathActive;
  private bool rockCrabEscaping;
  private bool rockCrabFinishing;
  private int rockCrabPendingTorpedos;
  private const string BubbleCrabVariant = "effect_bubblecrab_fish";
  private const string BlueFishVariant = "effect_blue_fish";
  private const string RockCrabVariant = "effect_rockcrab_fish";
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
  private bool blueFishFinishTriggered;
  private bool blueFishCoinBlastPlayed;
  private BaseFish blueFishLastTarget;
  private List<BaseFish> blueFishEffectTargets = new List<BaseFish>();

  internal override void Initialize(FishData data)
  {
    base.Initialize(data);
    UpdateRockCrabTorpedoVisual(data.variant == "effect_rockcrab_fish");
    SetupFallbackMovement();
    StartRockCrabSpeedVariation();
    StartBubbleCrabWhirlpool();
  }

  internal override void ResetFish()
  {
    bubbleCrabDeathActive = false;
    bubbleCrabAwaitingTorpedoImpact = false;
    bubbleCrabTorpedoImpactReceived = false;
    rockCrabDeathActive = false;
    rockCrabEscaping = false;
    rockCrabFinishing = false;
    rockCrabPendingTorpedos = 0;
    StopRockCrabTorpedoRocking();
    StopBubbleCrabWhirlpoolImage();
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
    if (rockCrabShakeRoutine != null)
    {
      StopCoroutine(rockCrabShakeRoutine);
      rockCrabShakeRoutine = null;
    }
    if (rockCrabEffectRoutine != null)
    {
      StopCoroutine(rockCrabEffectRoutine);
      rockCrabEffectRoutine = null;
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
    UpdateRockCrabTorpedoVisual(false);
    base.ResetFish();
  }

  private void StartRockCrabSpeedVariation()
  {
    if (data == null || data.variant != RockCrabVariant)
      return;

    if (segmentedSpeedRoutine != null)
      StopCoroutine(segmentedSpeedRoutine);

    segmentedSpeedRoutine = StartCoroutine(CrabSegmentedSpeedRoutine());
  }

  private void UpdateRockCrabTorpedoVisual(bool setActive)
  {
    if (rockCrabTorpedoVisual == null)
      return;

    rockCrabTorpedoVisual.SetActive(setActive);
    if (setActive)
      StartRockCrabTorpedoRocking();
    else
      StopRockCrabTorpedoRocking();
  }

  private void StartRockCrabTorpedoRocking()
  {
    if (rockCrabTorpedoVisual == null)
      return;
    if (rockCrabDeathActive || isDespawning)
      return;
    if (!rockCrabTorpedoVisual.activeInHierarchy)
      return;

    StopRockCrabTorpedoRocking();

    Transform visualTransform = rockCrabTorpedoVisual.transform;
    visualTransform.localRotation = Quaternion.Euler(0f, 0f, -rockCrabTorpedoRockAngle);
    rockCrabTorpedoRockTween = visualTransform
      .DOLocalRotate(new Vector3(0f, 0f, rockCrabTorpedoRockAngle), rockCrabTorpedoRockDuration)
      .SetEase(Ease.Linear)
      .SetLoops(-1, LoopType.Yoyo);
  }

  private void StopRockCrabTorpedoRocking()
  {
    if (rockCrabTorpedoRockTween != null)
    {
      rockCrabTorpedoRockTween.Kill();
      rockCrabTorpedoRockTween = null;
    }
    if (rockCrabTorpedoVisual != null)
      rockCrabTorpedoVisual.transform.localRotation = Quaternion.identity;
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


    if (bubbleCrabEffectRoutine != null)
      StopCoroutine(bubbleCrabEffectRoutine);
    bubbleCrabEffectRoutine = StartCoroutine(BubbleCrabEffectRoutine(affectedFishes));
  }

  internal void TriggerRockCrabDeath(List<BaseFish> affectedFishes)
  {
    if (data == null || data.variant != RockCrabVariant)
      return;

    if (rockCrabDeathActive)
      return;

    rockCrabDeathActive = true;
    MarkPendingDeath();
    StopPathMovement();
    FishManager.Instance?.MoveToAnimParent(this);
    StopRockCrabTorpedoRocking();

    if (segmentedSpeedRoutine != null)
    {
      StopCoroutine(segmentedSpeedRoutine);
      segmentedSpeedRoutine = null;
    }

    if (rockCrabEffectRoutine != null)
      StopCoroutine(rockCrabEffectRoutine);
    rockCrabEffectRoutine = StartCoroutine(RockCrabEffectRoutine(affectedFishes));
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
    yield return StartCoroutine(WaitForIncomingTorpedos());
    bubbleCrabBaseLocalPos = transform.localPosition;
    if (blueBlastRoutine != null)
      StopCoroutine(blueBlastRoutine);
    blueBlastRoutine = StartCoroutine(PlayBlueBlastEffectRoutine());
    yield return blueBlastRoutine;
    yield return new WaitForSecondsRealtime(0.5f);

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
    
    StartBubbleCrabWhirlpoolImage();
    if (bubbleCrabShakeRoutine != null)
      StopCoroutine(bubbleCrabShakeRoutine);
    bubbleCrabShakeRoutine = StartCoroutine(BubbleCrabShakeRoutine());

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
      PlayCoinBlast(fish);
      fish.ForceDespawn();
    }

    bubbleCrabDeathActive = false;
    PendingVisualDeath = false;
    ForceDespawn();
  }

  private IEnumerator RockCrabShakeRoutine()
  {
    while (rockCrabDeathActive)
    {
      float x = Mathf.PerlinNoise(Time.time * rockCrabShakeSpeed, 0f) * 2f - 1f;
      float y = Mathf.PerlinNoise(0f, Time.time * rockCrabShakeSpeed) * 2f - 1f;
      Vector3 offset = new Vector3(x, y, 0f) * rockCrabShakeRadius;
      transform.localPosition = rockCrabBaseLocalPos + offset;
      yield return null;
    }

    transform.localPosition = rockCrabBaseLocalPos;
  }

  private IEnumerator RockCrabEffectRoutine(List<BaseFish> affectedFishes)
  {
    yield return StartCoroutine(WaitForIncomingTorpedos());

    rockCrabBaseLocalPos = transform.localPosition;
    if (rockCrabShakeRoutine != null)
      StopCoroutine(rockCrabShakeRoutine);
    rockCrabShakeRoutine = StartCoroutine(RockCrabShakeRoutine());

    List<BaseFish> validTargets = CollectAffectedTargets(affectedFishes);

    if (rockCrabPreFireDelay > 0f)
      yield return new WaitForSeconds(rockCrabPreFireDelay);

    if (isDespawning || !rockCrabDeathActive)
      yield break;

    rockCrabPendingTorpedos = 0;
    int targetCount = validTargets.Count;
    for (int i = 0; i < targetCount; i++)
    {
      var fish = validTargets[i];
      if (isDespawning || !rockCrabDeathActive)
        break;
      if (fish == null)
        continue;

      rockCrabPendingTorpedos++;
      FireRockCrabTorpedo(fish);
      if (rockCrabTorpedoInterval > 0f)
        yield return new WaitForSeconds(rockCrabTorpedoInterval);
    }

    if (rockCrabPendingTorpedos > 0)
    {
      float timer = 0f;
      float timeout = Mathf.Max(0f, rockCrabTorpedoHitTimeout);
      while (rockCrabPendingTorpedos > 0 && timer < timeout)
      {
        if (isDespawning || !rockCrabDeathActive)
          yield break;
        timer += Time.deltaTime;
        yield return null;
      }
    }

    if (rockCrabTorpedoVisual != null)
    {
      rockCrabTorpedoVisual.SetActive(false);
      StopRockCrabTorpedoRocking();
    }

    rockCrabFinishing = true;
    rockCrabDeathActive = false;
    PendingVisualDeath = false;
    yield return new WaitForSecondsRealtime(1f);
    yield return StartCoroutine(RockCrabEscapeScaleRoutine());
    StartRockCrabEscape();
  }

  private void FireRockCrabTorpedo(BaseFish target)
  {
    if (CrabTorpedoPool.Instance == null)
      return;

    if (target == null)
      return;
    if (isDespawning || !rockCrabDeathActive)
      return;

    var torpedo = CrabTorpedoPool.Instance.GetFromPool();
    Vector3 launchPos = rockCrabTorpedoVisual != null
      ? rockCrabTorpedoVisual.transform.position
      : transform.position;
    torpedo.transform.SetPositionAndRotation(launchPos, Quaternion.identity);
    torpedo.Init(target, OnRockCrabTorpedoHit);
    PlayRockCrabLaunchBlast(launchPos);
  }

  private void OnRockCrabTorpedoHit(BaseFish target)
  {
    if (target == null)
      return;

    if (rockCrabPendingTorpedos > 0)
      rockCrabPendingTorpedos--;

    target.PendingVisualDeath = false;
    target.ForceDespawn();
  }


  private IEnumerator RockCrabEscapeScaleRoutine()
  {
    float duration = Mathf.Max(0.01f, rockCrabEscapeScaleDuration);
    float halfDuration = duration * 0.5f;
    Vector3 baseScale = transform.localScale;
    Vector3 targetScale = baseScale * Mathf.Max(0.01f, rockCrabEscapeScaleMultiplier);

    Tween upTween = transform
      .DOScale(targetScale, halfDuration)
      .SetEase(Ease.OutBack);
    yield return upTween.WaitForCompletion();

    Tween downTween = transform
      .DOScale(baseScale, halfDuration)
      .SetEase(Ease.InBack);
    yield return downTween.WaitForCompletion();
  }

  private void PlayRockCrabLaunchBlast(Vector3 pos)
  {
    if (BlastAnimationPool.Instance == null)
      return;

    var blast = BlastAnimationPool.Instance.GetFromPool();
    blast.transform.SetPositionAndRotation(pos, Quaternion.identity);
    blast.StartAnimation();
    blast.OnAnimationComplete = () =>
    {
      BlastAnimationPool.Instance.ReturnToPool(blast);
    };
  }

  private void StartRockCrabEscape()
  {
    if (splineController == null)
      return;

    rockCrabEscaping = true;
    rockCrabFinishing = false;
    splineController.enabled = true;
    splineController.PlayAutomatically = true;
    SetSpeedMultiplier(rockCrabEscapeSpeedMultiplier);
    splineController.Play();
  }

  internal bool IsTorpedoTargetable =>
    !rockCrabDeathActive && !rockCrabFinishing && !rockCrabEscaping;
  internal bool IgnoreExpiredCleanup =>
    PendingVisualDeath ||
    bubbleCrabDeathActive ||
    blueFishDeathActive ||
    rockCrabDeathActive ||
    rockCrabFinishing ||
    rockCrabEscaping;

  private IEnumerator WaitForIncomingTorpedos()
  {
    if (ActiveTorpedoCount <= 0)
      yield break;

    float timer = 0f;
    float timeout = Mathf.Max(0f, effectFishTorpedoWaitTimeout);
    while (ActiveTorpedoCount > 0 && timer < timeout)
    {
      if (isDespawning)
        yield break;
      timer += Time.deltaTime;
      yield return null;
    }
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
    yield return StartCoroutine(WaitForIncomingTorpedos());

    List<BaseFish> validTargets = CollectAffectedTargets(affectedFishes);
    blueFishFinishTriggered = false;
    blueFishCoinBlastPlayed = false;
    blueFishLastTarget = validTargets.Count > 0 ? validTargets[validTargets.Count - 1] : null;
    blueFishEffectTargets = validTargets;

    if (blueBlastRoutine != null)
      StopCoroutine(blueBlastRoutine);
    blueBlastRoutine = StartCoroutine(PlayBlueBlastEffectRoutine());
    yield return blueBlastRoutine;
    StartBlueFishShake();
    blueBlastRoutine = null;

    SetAnimationSpeedMultiplier(blueFishAnimationSpeedMultiplier);

    if (validTargets.Count > 0)
      yield return StartCoroutine(ChainLightningRoutine(validTargets));

    if (!blueFishCoinBlastPlayed)
    {
      PlayCoinBlast(this);
      foreach (var fish in validTargets)
        PlayCoinBlast(fish);
    }

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
    StopBlueFishShake();
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
      if (blueFishFinishTriggered)
        break;
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

    if (!blueFishFinishTriggered)
      StartMovingAffectedFish(source);

    if (!blueFishFinishTriggered && blueFishMovingCount > 0)
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

  private IEnumerator PlayBlueBlastEffectRoutine()
  {
    if (BlueBlastEffectPool.Instance == null)
    {
      yield break;
    }

    bool pooled;
    ImageAnimation effect = GetBlueBlastEffect(out pooled);
    if (effect == null)
    {
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

    yield return new WaitUntil(() => finished);

    effect.OnAnimationComplete -= handleComplete;
    ReturnBlueBlastEffect(effect, pooled);
    blueFishTransientEffects.RemoveAll(entry => entry.anim == effect);
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
      if (!blueFishFinishTriggered &&
          fish == blueFishLastTarget &&
          blueFishExplosionDistance > 0f &&
          Vector3.Distance(fish.transform.position, targetPos) <= blueFishExplosionDistance)
      {
        TriggerBlueFishFinish();
      }
      yield return null;
    }

    if (fish != null)
      fish.transform.position = targetPos;

    StopSpinForTarget(fish);

    blueFishMoveRoutines.Remove(fish);
    blueFishMovingCount = Mathf.Max(0, blueFishMovingCount - 1);
  }

  private void TriggerBlueFishFinish()
  {
    if (blueFishCoinBlastPlayed)
      return;

    blueFishFinishTriggered = true;
    blueFishCoinBlastPlayed = true;

    foreach (var pair in blueFishMoveRoutines)
    {
      if (pair.Value != null)
        StopCoroutine(pair.Value);
    }
    blueFishMoveRoutines.Clear();
    blueFishMovingCount = 0;

    foreach (var pair in blueFishSpinRoutines)
    {
      if (pair.Value != null)
        StopCoroutine(pair.Value);
    }
    blueFishSpinRoutines.Clear();

    if (blueFishEffectTargets != null)
    {
      PlayCoinBlast(this);
      foreach (var fish in blueFishEffectTargets)
        PlayCoinBlast(fish);
    }
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

  private void StartBubbleCrabWhirlpoolImage()
  {
    if (bubbleCrabWhirlpoolImage == null)
      return;

    bubbleCrabWhirlpoolImage.gameObject.SetActive(true);
    bubbleCrabWhirlpoolImage.color = new Color(
      bubbleCrabWhirlpoolImage.color.r,
      bubbleCrabWhirlpoolImage.color.g,
      bubbleCrabWhirlpoolImage.color.b,
      0f
    );
    bubbleCrabWhirlpoolImage.rectTransform.localRotation = Quaternion.identity;

    bubbleCrabWhirlpoolFadeTween?.Kill();
    bubbleCrabWhirlpoolRotateTween?.Kill();

    bubbleCrabWhirlpoolFadeTween = bubbleCrabWhirlpoolImage
      .DOFade(0.8f, bubbleCrabWhirlpoolFadeInDuration)
      .SetEase(Ease.OutQuad);
    bubbleCrabWhirlpoolRotateTween = bubbleCrabWhirlpoolImage.rectTransform
      .DORotate(new Vector3(0f, 0f, -360f), bubbleCrabWhirlpoolRotateDuration, RotateMode.FastBeyond360)
      .SetEase(Ease.Linear)
      .SetLoops(-1);
  }

  private void StopBubbleCrabWhirlpoolImage()
  {
    bubbleCrabWhirlpoolFadeTween?.Kill();
    bubbleCrabWhirlpoolRotateTween?.Kill();
    bubbleCrabWhirlpoolFadeTween = null;
    bubbleCrabWhirlpoolRotateTween = null;

    if (bubbleCrabWhirlpoolImage == null)
      return;

    bubbleCrabWhirlpoolImage.rectTransform.localRotation = Quaternion.identity;
    bubbleCrabWhirlpoolImage.gameObject.SetActive(false);
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
