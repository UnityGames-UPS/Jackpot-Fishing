using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

internal class ImmortalBucketAnimView : MonoBehaviour
{
  [Header("Big Bucket")]
  [SerializeField] private RectTransform bigBucketRect;
  [SerializeField] private ImageAnimation bigBucketAnim;
  [SerializeField] private ImageAnimation glowAnim;
  [SerializeField] private float bigRiseY = 120f;
  [SerializeField] private float bigFallY = -40f;
  [SerializeField] private float bigRiseDuration = 0.35f;
  [SerializeField] private float bigFallDuration = 0.35f;
  [SerializeField] private float bigScaleMultiplier = 1.3f;

  [Header("Small Buckets")]
  [SerializeField] private List<SmallBucket> smallBuckets = new List<SmallBucket>();
  [SerializeField] private float smallSpreadX = 80f;
  [SerializeField] private float smallRiseY = 50f;
  [SerializeField] private float smallFallY = -20f;
  [SerializeField] private float smallRiseDuration = 0.3f;
  [SerializeField] private float smallFallDuration = 0.3f;
  [SerializeField] private float smallRandomX = 90f;
  [SerializeField] private float smallRandomY = 60f;
  [SerializeField] private float smallRandomRiseY = 45f;
  [SerializeField] private float smallRandomFallY = 30f;
  [SerializeField] private float smallRandomRiseDuration = 0.12f;
  [SerializeField] private float smallRandomFallDuration = 0.12f;

  private Vector3 bigBaseScale;
  private Tween bigTween;
  private readonly List<Tween> smallTweens = new List<Tween>();
  private Coroutine returnRoutine;

  private System.Action onReturned;

  private void Awake()
  {
    CacheScales();
  }

  private void OnDisable()
  {
    Cleanup();
  }

  internal void Play(Vector3 worldPosition, System.Action onComplete)
  {
    Cleanup();
    onReturned = onComplete;

    transform.position = worldPosition;
    ResetBuckets();

    StartBucketAnimations();
    StartBucketMovement();

  }

  internal void ReturnToPool()
  {
    Cleanup();
    onReturned?.Invoke();
    onReturned = null;

    var pool = ImmortalBucketAnimPool.Instance;
    if (pool != null)
      pool.ReturnToPool(this);
    else
      gameObject.SetActive(false);
  }

  private void StartBucketAnimations()
  {
    if (glowAnim != null)
    {
      glowAnim.doLoopAnimation = false;
      glowAnim.OnAnimationComplete = HandleGlowComplete;
      glowAnim.StartAnimation();
    }

    if (bigBucketAnim != null)
    {
      bigBucketAnim.doLoopAnimation = true;
      bigBucketAnim.StartAnimation();
    }

    for (int i = 0; i < smallBuckets.Count; i++)
    {
      var bucket = smallBuckets[i];
      if (bucket.anim == null)
        continue;
      bucket.anim.doLoopAnimation = true;
      bucket.anim.StartAnimation();
    }
  }

  private void StartBucketMovement()
  {
    if (bigBucketRect != null)
    {
      bigTween = DOTween.Sequence()
        .Append(bigBucketRect.DOLocalMoveY(bigRiseY, bigRiseDuration).SetEase(Ease.OutQuad))
        .Append(bigBucketRect.DOLocalMoveY(bigFallY, bigFallDuration).SetEase(Ease.InQuad))
        .OnComplete(() =>
        {
          returnRoutine = StartCoroutine(ReturnAfterDelay(1f));
        });
    }

    int count = smallBuckets.Count;
    for (int i = 0; i < count; i++)
    {
      var bucket = smallBuckets[i];
      if (bucket.rect == null)
        continue;

      float sign = (i % 2 == 0) ? -1f : 1f;
      float spreadStep = 1f + (i / 2) * 0.1f;
      float randomX = Random.Range(-smallRandomX, smallRandomX);
      float randomY = Random.Range(-smallRandomY, smallRandomY);
      float randomRise = Random.Range(-smallRandomRiseY, smallRandomRiseY);
      float randomFall = Random.Range(-smallRandomFallY, smallRandomFallY);
      float targetX = smallSpreadX * sign * spreadStep + randomX;
      float riseY = smallRiseY + randomY + randomRise;
      float fallY = smallFallY + randomY * 0.5f + randomFall;
      float riseDuration = Mathf.Max(0.01f, smallRiseDuration + Random.Range(-smallRandomRiseDuration, smallRandomRiseDuration));
      float fallDuration = Mathf.Max(0.01f, smallFallDuration + Random.Range(-smallRandomFallDuration, smallRandomFallDuration));

      bucket.rect.gameObject.SetActive(true);
      Tween tween = DOTween.Sequence()
        .Append(bucket.rect.DOLocalMove(new Vector3(targetX, riseY, 0f), riseDuration)
          .SetEase(Ease.OutQuad))
        .Append(bucket.rect.DOLocalMove(new Vector3(targetX, fallY, 0f), fallDuration)
          .SetEase(Ease.InQuad))
        .OnComplete(() => bucket.rect.gameObject.SetActive(false));

      smallTweens.Add(tween);
    }
  }

  private IEnumerator ReturnAfterDelay(float delay)
  {
    yield return new WaitForSecondsRealtime(delay);
    ReturnToPool();
  }

  private void ResetBuckets()
  {
    if (bigBucketRect != null)
    {
      bigBucketRect.localPosition = Vector3.zero;
      bigBucketRect.localScale = bigBaseScale * Mathf.Max(0.01f, bigScaleMultiplier);
      bigBucketRect.gameObject.SetActive(true);
    }

    for (int i = 0; i < smallBuckets.Count; i++)
    {
      var bucket = smallBuckets[i];
      if (bucket.rect == null)
        continue;
      bucket.rect.localPosition = Vector3.zero;
      bucket.rect.localScale = bucket.baseScale;
      bucket.rect.gameObject.SetActive(true);
    }
  }

  private void CacheScales()
  {
    if (bigBucketRect != null)
      bigBaseScale = bigBucketRect.localScale;

    for (int i = 0; i < smallBuckets.Count; i++)
    {
      var bucket = smallBuckets[i];
      if (bucket.rect == null)
        continue;
      bucket.baseScale = bucket.rect.localScale;
    }
  }

  private void Cleanup()
  {
    if (returnRoutine != null)
    {
      StopCoroutine(returnRoutine);
      returnRoutine = null;
    }

    bigTween?.Kill();
    bigTween = null;

    for (int i = 0; i < smallTweens.Count; i++)
      smallTweens[i]?.Kill();
    smallTweens.Clear();

    if (bigBucketAnim != null)
      bigBucketAnim.StopAnimation();
    if (glowAnim != null)
    {
      glowAnim.OnAnimationComplete = null;
      glowAnim.StopAnimation();
    }

    for (int i = 0; i < smallBuckets.Count; i++)
    {
      var bucket = smallBuckets[i];
      if (bucket.anim != null)
        bucket.anim.StopAnimation();
    }
  }

  private void HandleGlowComplete()
  {
    if (glowAnim == null)
      return;
    glowAnim.StopAnimation();
  }
}
