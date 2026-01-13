using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using FluffyUnderware.Curvy;
using UnityEngine;

internal class NormalFish : BaseFish
{

  private Coroutine segmentedSpeedRoutine;

  internal void Initialize(FishData data, SpawnBatchContext context)
  {
    base.Initialize(data);
    if (context == null)
    {
      SetupFallbackMovement();
    }
    else if (data.variant != "bluewing_fish" &&
             data.variant != "hammer_head" &&
             data.variant != "saw_fish")
    {
      SetupCurvyMovement(context);
    }
    else
    {
      SetupFallbackMovement();
    }
    segmentedSpeedRoutine = StartCoroutine(SegmentedSpeedRoutine());
  }

  private void SetupCurvyMovement(SpawnBatchContext batch)
  {
    bool moveRightToLeft = batch.moveRightToLeft;
    FlipSprite(faceRight: !moveRightToLeft);

    PathSet chosenSet = null;

    if (batch.usePathSet)
    {
      if (batch.chosenPathSet == null)
      {
        var pathSets =
          CurvyPathProvider.Instance.GetPathSetsForType(data.fishType);

        if (pathSets != null && pathSets.Count > 0)
          batch.chosenPathSet =
            pathSets[Random.Range(0, pathSets.Count)];
      }

      chosenSet = batch.chosenPathSet;
    }

    List<CurvySpline> splines = chosenSet != null
      ? (moveRightToLeft
          ? chosenSet.rightToLeft
          : chosenSet.leftToRight)
      : CurvyPathProvider.Instance.GetFallbackSplines(moveRightToLeft);

    CurvySpline chosenSpline = null;

    foreach (var s in splines)
    {
      if (!batch.IsSplineUsed(s))
      {
        chosenSpline = s;
        batch.MarkSplineUsed(s);
        break;
      }
    }

    if (chosenSpline == null)
      chosenSpline = splines[Random.Range(0, splines.Count)];

    ApplySpline(chosenSpline, moveRightToLeft);
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

  private IEnumerator SegmentedSpeedRoutine()
  {
    // Safety: wait until spline is ready
    yield return null;

    if (splineController == null || splineController.Spline == null)
      yield break;

    float pathLength = splineController.Length;
    if (pathLength <= 0f)
      yield break;

    const float minSegmentDistance = 5f;
    const float maxSegmentDistance = 20f;
    const float minSpeedMultiplier = 0.85f;
    const float maxSpeedMultiplier = 1.15f;

    List<float> segmentDistances = new List<float>();
    List<float> segmentMultipliers = new List<float>();

    float remainingDistance = pathLength;
    while (remainingDistance > 0f)
    {
      float segmentDistance = Random.Range(minSegmentDistance, maxSegmentDistance);
      if (segmentDistance > remainingDistance)
        segmentDistance = remainingDistance;

      segmentDistances.Add(segmentDistance);
      segmentMultipliers.Add(Random.Range(minSpeedMultiplier, maxSpeedMultiplier));
      remainingDistance -= segmentDistance;
    }

    float weightedTimeSum = 0f;
    for (int i = 0; i < segmentDistances.Count; i++)
      weightedTimeSum += segmentDistances[i] / segmentMultipliers[i];

    float scaleFactor = weightedTimeSum > 0f ? weightedTimeSum / pathLength : 1f;

    float targetPos = splineController.Position;
    for (int i = 0; i < segmentDistances.Count; i++)
    {
      if (splineController == null)
        break;

      targetPos = Mathf.Min(targetPos + segmentDistances[i], splineController.Length);
      SetSpeedMultiplier(segmentMultipliers[i] * scaleFactor);

      yield return new WaitUntil(() =>
        splineController == null ||
        splineController.Position >= targetPos ||
        splineController.Position >= splineController.Length
      );
    }

    // Restore baseline at end (important for pooling)
    SetSpeedMultiplier(1f);
  }

  internal override void Die()
  {
    if (!BeginDeath())
      return;

    Sequence dieSeq = DOTween.Sequence();
    dieSeq.AppendInterval(0.1f);

    float currentY = transform.localPosition.y;
    float jumpHeight = 100f;

    dieSeq.Append(transform.DOLocalMoveY(currentY + jumpHeight, 0.15f).SetEase(Ease.OutQuad));
    dieSeq.Append(transform.DOLocalMoveY(currentY, 0.15f).SetEase(Ease.InQuad));

    dieSeq.OnComplete(() =>
    {
      if (!PendingVisualDeath)
        DespawnFish();
    });
  }
}
