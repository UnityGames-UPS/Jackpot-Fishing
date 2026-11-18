using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public class HitMarker : MonoBehaviour
{
  [SerializeField] private Image image;
  [SerializeField] private float scaleDuration = 0.08f;
  [SerializeField] private float fadeDelay = 0.05f;
  [SerializeField] private float fadeDuration = 0.15f;

  [Header("Bet Tier Art")]
  [SerializeField] private TierSprites markerSprites;

  private Tween tween;
  
  // tier: the bet tier of the bullet that hit, which picks the marker's colour.
  internal void Play(Vector3 worldPos, GunTier tier = GunTier.Base)
  {
    tween?.Kill();
    markerSprites.ApplyTo(image, tier);

    transform.position = worldPos;
    transform.localScale = Vector3.zero;

    image.color = Color.white;
    gameObject.SetActive(true);

    tween = DOTween.Sequence()
        .Append(transform.DOScale(1f, scaleDuration).SetEase(Ease.OutBack))
        .AppendInterval(fadeDelay)
        .Append(image.DOFade(0f, fadeDuration))
        .OnComplete(ReturnToPool);
  }

  private void ReturnToPool()
  {
    tween?.Kill();
    HitMarkerPool.Instance.ReturnToPool(this);
  }
}
