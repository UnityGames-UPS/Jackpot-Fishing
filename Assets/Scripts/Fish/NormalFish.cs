using DG.Tweening;
using UnityEngine;

internal class NormalFish : BaseFish
{
  [SerializeField] Color redColor;
  internal override void DamageAnimation()
  {
    if (fishImage == null) return;

    damageTween?.Kill();

    damageTween = DOTween.Sequence()
        .Append(fishImage.DOColor(redColor, 0.05f))
        .AppendInterval(0.3f)
        .Append(fishImage.DOColor(Color.white, 0.1f));
  }

  internal override void Die()
  {
    boxCollider.enabled = false;

    if (splineController != null)
    {
      splineController.PlayAutomatically = false;
      splineController.Pause(); 
    }

    Sequence dieSeq = DOTween.Sequence();
    dieSeq.AppendInterval(0.1f);

    float currentY = transform.localPosition.y;
    float jumpHeight = 100f;

    dieSeq.Append(transform.DOLocalMoveY(currentY + jumpHeight, 0.15f).SetEase(Ease.OutQuad));
    dieSeq.Append(transform.DOLocalMoveY(currentY, 0.15f).SetEase(Ease.InQuad));

    dieSeq.OnComplete(() =>
    {
      DespawnFish();
    });
  }
}
