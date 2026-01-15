using UnityEngine;
using DG.Tweening;
using TMPro;

public class RefundTextPopup : MonoBehaviour
{
  [SerializeField] private TMP_Text label;
  [SerializeField] private float riseDistance = 0.6f;
  [SerializeField] private float riseDuration = 0.45f;
  [SerializeField] private float fadeDuration = 0.2f;
  [SerializeField] private float fadeOutDealy = 1f;

  private Tween tween;
  private CanvasGroup canvasGroup;

  private void Awake()
  {
    if (label == null)
      label = GetComponentInChildren<TMP_Text>();

    canvasGroup = GetComponent<CanvasGroup>();
    if (canvasGroup == null)
      canvasGroup = gameObject.AddComponent<CanvasGroup>();
  }

  internal void Play(Vector3 worldPos, float amount)
  {
    tween?.Kill();

    transform.position = worldPos;
    transform.localScale = Vector3.one;

    if (label != null)
      label.text = $"{amount:0.##}";

    canvasGroup.alpha = 0f;
    gameObject.SetActive(true);

    Vector3 endPos = worldPos + Vector3.up * riseDistance;
    tween = DOTween.Sequence()
      .Append(canvasGroup.DOFade(1f, fadeDuration))
      .AppendInterval(fadeOutDealy)
      .Append(transform.DOMove(endPos, riseDuration).SetEase(Ease.OutQuad))
      .Join(canvasGroup.DOFade(0f, fadeDuration))
      .OnComplete(ReturnToPool);
  }

  private void ReturnToPool()
  {
    tween?.Kill();
    RefundTextPool.Instance.ReturnToPool(this);
  }
}
