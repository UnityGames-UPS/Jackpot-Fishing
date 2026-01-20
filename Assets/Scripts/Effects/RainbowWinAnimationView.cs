using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

internal class RainbowWinAnimationView : MonoBehaviour
{
  [Header("Image Animations")]
  [SerializeField] private ImageAnimation rainbowCircleAnimation;
  [SerializeField] private ImageAnimation blastAnimation;
  [SerializeField] private ImageAnimation fishAnimation;

  [Header("Win Text")]
  [SerializeField] private TMP_Text winAmountLabel;

  [Header("Timing")]
  [SerializeField] private float appearDuration = 0.18f;
  [SerializeField] private Ease appearEase = Ease.OutBack;
  [SerializeField] private float textAppearDuration = 0.22f;
  [SerializeField] private Ease textAppearEase = Ease.OutBack;
  [SerializeField] private float pulseScale = 0.12f;
  [SerializeField] private float pulseDuration = 0.55f;
  [SerializeField] private float ttl = 2.5f;
  [SerializeField] private float fishSpeedMultiplier = 2f;

  private Vector3 rainbowInitScale;
  private Vector3 blastInitScale;
  private Vector3 fishInitScale;
  private Vector3 textInitScale;
  private Tween rainbowTween;
  private Tween blastTween;
  private Tween fishTween;
  private Tween textTween;
  private Tween pulseTween;
  private Coroutine ttlRoutine;
  private bool scalesCached;

  private void Awake()
  {
    CacheScales();
    ResetVisuals();
  }

  private void OnDisable()
  {
    CleanupTweens();
    StopAnimations();
    StopTtlRoutine();
  }

  internal void Play(BaseFish fish, FishData fishData, float winAmount)
  {
    if (fish == null || fishData == null || winAmount <= 0)
      return;

    CleanupTweens();
    StopAnimations();
    StopTtlRoutine();

    CacheScales();
    ResetVisuals();

    Vector3 spawnPos = ResolveSpawnPosition(fish);
    transform.SetPositionAndRotation(spawnPos, Quaternion.identity);

    if (winAmountLabel != null)
      winAmountLabel.text = winAmount.ToString("0.##");

    if (fishAnimation != null && fishData.animationFrames != null && fishData.animationFrames.Length > 0)
    {
      float speed = Mathf.Max(0.01f, fishData.animationSpeed * Mathf.Max(0.01f, fishSpeedMultiplier));
      fishAnimation.SetAnimationData(fishData.animationFrames, speed, fishData.loop);
      fishAnimation.StartAnimation();
    }

    if (rainbowCircleAnimation != null)
      rainbowCircleAnimation.StartAnimation();

    if (blastAnimation != null)
    {
      blastAnimation.doLoopAnimation = false;
      blastAnimation.OnAnimationComplete = HandleBlastComplete;
      blastAnimation.StartAnimation();
    }
    else
    {
      HandleBlastComplete();
    }

    rainbowTween = ScaleUp(rainbowCircleAnimation, rainbowInitScale);
    blastTween = ScaleUp(blastAnimation, blastInitScale);
    fishTween = ScaleUp(fishAnimation, fishInitScale);

    if (ttl > 0f)
      ttlRoutine = StartCoroutine(TtlReturnRoutine(ttl));
  }

  private Vector3 ResolveSpawnPosition(BaseFish fish)
  {
    RectTransform spawnArea = transform.parent as RectTransform;
    if (spawnArea == null)
      return fish.ColliderMidPoint;

    Vector3[] corners = new Vector3[4];
    spawnArea.GetWorldCorners(corners);
    float minX = corners[0].x;
    float maxX = corners[2].x;
    float minY = corners[0].y;
    float maxY = corners[2].y;
    float x = Random.Range(minX, maxX);
    float y = Random.Range(minY, maxY);
    return new Vector3(x, y, fish.ColliderMidPoint.z);
  }

  internal void ReturnToPool()
  {
    CleanupTweens();
    StopAnimations();
    StopTtlRoutine();

    var pool = RainbowAnimationPool.Instance;
    if (pool != null)
    {
      pool.ReturnToPool(this);
      return;
    }

    gameObject.SetActive(false);
  }

  private void HandleBlastComplete()
  {
    if (winAmountLabel == null)
      return;

    winAmountLabel.gameObject.SetActive(true);
    winAmountLabel.transform.localScale = Vector3.zero;

    textTween = winAmountLabel.transform
      .DOScale(textInitScale, textAppearDuration)
      .SetEase(textAppearEase)
      .OnComplete(StartPulse);
  }

  private void StartPulse()
  {
    if (winAmountLabel == null)
      return;

    pulseTween?.Kill();
    Vector3 pulseTarget = textInitScale * (1f + Mathf.Max(0f, pulseScale));
    pulseTween = winAmountLabel.transform
      .DOScale(pulseTarget, pulseDuration)
      .SetEase(Ease.InOutSine)
      .SetLoops(-1, LoopType.Yoyo);
  }

  private Tween ScaleUp(Component target, Vector3 targetScale)
  {
    if (target == null)
      return null;

    target.transform.localScale = Vector3.zero;
    return target.transform.DOScale(targetScale, appearDuration).SetEase(appearEase);
  }

  private IEnumerator TtlReturnRoutine(float wait)
  {
    yield return new WaitForSeconds(wait);
    ReturnToPool();
  }

  private void CacheScales()
  {
    if (scalesCached)
      return;

    if (rainbowCircleAnimation != null)
      rainbowInitScale = rainbowCircleAnimation.transform.localScale;
    if (blastAnimation != null)
      blastInitScale = blastAnimation.transform.localScale;
    if (fishAnimation != null)
      fishInitScale = fishAnimation.transform.localScale;
    if (winAmountLabel != null)
      textInitScale = winAmountLabel.transform.localScale;

    scalesCached = true;
  }

  private void ResetVisuals()
  {
    SetScale(rainbowCircleAnimation, Vector3.zero);
    SetScale(blastAnimation, Vector3.zero);
    SetScale(fishAnimation, Vector3.zero);

    if (winAmountLabel != null)
    {
      winAmountLabel.gameObject.SetActive(false);
      winAmountLabel.transform.localScale = Vector3.zero;
    }
  }

  private void SetScale(Component target, Vector3 scale)
  {
    if (target == null)
      return;
    target.transform.localScale = scale;
  }

  private void StopAnimations()
  {
    if (blastAnimation != null)
    {
      blastAnimation.OnAnimationComplete = null;
      blastAnimation.StopAnimation();
    }

    rainbowCircleAnimation?.StopAnimation();
    fishAnimation?.StopAnimation();
  }

  private void CleanupTweens()
  {
    rainbowTween?.Kill();
    blastTween?.Kill();
    fishTween?.Kill();
    textTween?.Kill();
    pulseTween?.Kill();
    rainbowTween = null;
    blastTween = null;
    fishTween = null;
    textTween = null;
    pulseTween = null;
  }

  private void StopTtlRoutine()
  {
    if (ttlRoutine != null)
    {
      StopCoroutine(ttlRoutine);
      ttlRoutine = null;
    }
  }
}
