using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class BubbleWhirlpoolBubble : MonoBehaviour
{
  [SerializeField] private float peakScaleMultiplier = 2f;
  [SerializeField, Range(0.01f, 0.99f)] private float peakScaleAtPercent = 0.5f;
  private RectTransform rectTransform;
  private Image bubbleImage;
  private Tween fadeTween;
  private Tween scaleTween;
  private BubbleWhirlpoolPool pool;
  private Vector3 initialScale;
  private float angleRad;
  private float radius;
  private float radialSpeed;
  private float angularSpeedRad;
  private float returnDistance;
  private float startRadius;

  private void Awake()
  {
    rectTransform = GetComponent<RectTransform>();
    bubbleImage = GetComponent<Image>();
    initialScale = transform.localScale;
  }

  private void OnDisable()
  {
    fadeTween?.Kill();
    fadeTween = null;
    scaleTween?.Kill();
    scaleTween = null;
    transform.localScale = initialScale;
  }

  internal void Init(
    BubbleWhirlpoolPool ownerPool,
    float startRadius,
    float startAngleRad,
    float radialSpeedPerSec,
    float angularSpeedRadPerSec,
    float returnDistanceThreshold)
  {
    pool = ownerPool;
    radius = Mathf.Max(0f, startRadius);
    this.startRadius = radius;
    angleRad = startAngleRad;
    radialSpeed = radialSpeedPerSec;
    angularSpeedRad = angularSpeedRadPerSec;
    returnDistance = Mathf.Max(0f, returnDistanceThreshold);
    SetupFade();
    UpdateLocalPosition();
  }

  private void Update()
  {
    if (pool == null)
      return;

    angleRad += angularSpeedRad * Time.deltaTime;
    radius -= radialSpeed * Time.deltaTime;

    if (radius <= returnDistance)
    {
      pool.ReturnToPool(this);
      return;
    }

    UpdateLocalPosition();
  }

  private void UpdateLocalPosition()
  {
    float x = Mathf.Cos(angleRad) * radius;
    float y = Mathf.Sin(angleRad) * radius;
    if (rectTransform != null)
    {
      rectTransform.anchoredPosition = new Vector2(x, y);
      return;
    }

    transform.localPosition = new Vector3(x, y, 0f);
  }

  private void SetupFade()
  {
    if (bubbleImage == null)
      return;

    fadeTween?.Kill();
    bubbleImage.color = new Color(1f, 1f, 1f, 0f);

    float totalDistance = Mathf.Max(0f, startRadius - returnDistance);
    if (totalDistance <= Mathf.Epsilon || radialSpeed <= Mathf.Epsilon)
      return;

    float travelTime = totalDistance / radialSpeed;
    float fadeSegment = travelTime * 0.1f;
    float middleTime = Mathf.Max(0f, travelTime - (fadeSegment * 2f));

    fadeTween = DOTween.Sequence()
      .Append(bubbleImage.DOFade(1f, fadeSegment).SetEase(Ease.Linear))
      .AppendInterval(middleTime)
      .Append(bubbleImage.DOFade(0f, fadeSegment).SetEase(Ease.Linear));

    scaleTween?.Kill();
    Vector3 baseScale = initialScale;
    float upTime = travelTime * peakScaleAtPercent;
    float downTime = Mathf.Max(0f, travelTime - upTime);
    if (upTime <= Mathf.Epsilon)
    {
      transform.localScale = baseScale;
      return;
    }

    scaleTween = DOTween.Sequence()
      .Append(transform.DOScale(baseScale * peakScaleMultiplier, upTime).SetEase(Ease.OutQuad))
      .Append(transform.DOScale(baseScale, downTime).SetEase(Ease.InQuad));
  }
}
