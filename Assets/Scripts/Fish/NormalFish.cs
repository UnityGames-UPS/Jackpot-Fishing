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
}
