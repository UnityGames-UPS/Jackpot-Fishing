using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal class EffectFish : BaseFish
{
  [SerializeField, Range(0f, 0.49f)] private float crabEdgePercentMin = 0.25f;
  [SerializeField, Range(0f, 0.49f)] private float crabEdgePercentMax = 0.4f;
  [SerializeField] private BubbleWhirlpoolController bubbleWhirlpool;
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
  private const float MinSpeedMultiplier = 0.6f;
  private const float MaxSpeedMultiplier = 1.6f;
  private Coroutine segmentedSpeedRoutine;
  private Coroutine bubbleCrabShakeRoutine;
  private Coroutine bubbleCrabEffectRoutine;
  private Vector3 bubbleCrabBaseLocalPos;
  private bool bubbleCrabDeathActive;
  private bool bubbleCrabAwaitingTorpedoImpact;
  private bool bubbleCrabTorpedoImpactReceived;
  private const string BubbleCrabVariant = "effect_bubblecrab_fish";

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
    List<BaseFish> validTargets = new List<BaseFish>();
    if (affectedFishes != null)
    {
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
    }

    int remaining = validTargets.Count;
    if (remaining > 0)
    {
      foreach (var fish in validTargets)
        StartCoroutine(OrbitAffectedFishAroundCrab(fish, () => remaining--));

      yield return new WaitUntil(() => remaining <= 0);
    }

    foreach (var fish in validTargets)
    {
      fish.PendingVisualDeath = false;
      fish.ForceDespawn();
    }

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

    PlayCoinBlast(this);
    bubbleCrabDeathActive = false;
    PendingVisualDeath = false;
    ForceDespawn();
  }

  private void CacheAndReparentAffectedFish(BaseFish fish)
  {
    if (fish == null)
      return;

    FishManager.Instance?.MoveToAnimParent(fish);
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
    float scale = fish.data.coinBlastScaleMult <= 0f
      ? 1f
      : fish.data.coinBlastScaleMult;
    coinAnimation.transform.localScale = Vector3.one * scale;
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
