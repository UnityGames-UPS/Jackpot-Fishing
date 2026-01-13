using System.Collections;
using UnityEngine;

internal class EffectFish : BaseFish
{
  [SerializeField, Range(0f, 0.49f)] private float crabEdgePercentMin = 0.15f;
  [SerializeField, Range(0f, 0.49f)] private float crabEdgePercentMax = 0.25f;
  private const float MinSpeedMultiplier = 0.6f;
  private const float MaxSpeedMultiplier = 1.6f;
  private Coroutine segmentedSpeedRoutine;

  internal override void Initialize(FishData data)
  {
    base.Initialize(data);
    SetupFallbackMovement();
    StartCrabSpeedVariation();
  }

  internal override void ResetFish()
  {
    base.ResetFish();
    if (segmentedSpeedRoutine != null)
    {
      StopCoroutine(segmentedSpeedRoutine);
      segmentedSpeedRoutine = null;
    }
  }

  private void StartCrabSpeedVariation()
  {
    if (data == null || data.variant != "effect_rockcrab_fish")
      return;

    if (segmentedSpeedRoutine != null)
      StopCoroutine(segmentedSpeedRoutine);

    segmentedSpeedRoutine = StartCoroutine(CrabSegmentedSpeedRoutine());
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
