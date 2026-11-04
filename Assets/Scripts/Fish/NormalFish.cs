using System;
using DG.Tweening;
using UnityEngine;

internal class NormalFish : BaseFish
{
  protected override void PlayDeath(Action onComplete)
  {
    float currentY = transform.localPosition.y;
    const float jumpHeight = 100f;

    Sequence dieSeq = DOTween.Sequence()
      .AppendInterval(0.1f)
      .Append(transform.DOLocalMoveY(currentY + jumpHeight, 0.15f).SetEase(Ease.OutQuad))
      .Append(transform.DOLocalMoveY(currentY, 0.15f).SetEase(Ease.InQuad))
      .Append(fishImage.DOFade(0f, 0.15f));

    BindLifecycle(dieSeq, onComplete);
  }
}
