using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using FluffyUnderware.Curvy;
using FluffyUnderware.Curvy.Controllers;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(SplineController))]
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(ImageAnimation))]
[RequireComponent(typeof(BoxCollider2D))]
internal class BaseFish : MonoBehaviour
{
  [SerializeField] internal FishData data;

  internal Transform HitPoint => transform.GetChild(0);
  internal RectTransform Rect => GetComponent<RectTransform>();

  protected Tween movementTween;
  protected Tween damageTween;

  protected ImageAnimation imageAnimation;
  protected Image fishImage;
  protected BoxCollider2D boxCollider;
  protected SplineController splineController;

  protected float baseSpeed;
  protected float speedMultiplier = 1f;

  private Coroutine lifeTimeoutRoutine;
  private bool isDespawning;

#if UNITY_EDITOR
  private float spawnTime; // optional debug
#endif

  internal virtual void DamageAnimation() { }

  internal virtual void Die() { }

  // --------------------------------------------------------
  // INIT
  // --------------------------------------------------------
  internal virtual void Initialize(FishData data)
  {
    this.data = data;

#if UNITY_EDITOR
    spawnTime = Time.realtimeSinceStartup;
#endif

    splineController = GetComponent<SplineController>();
    imageAnimation = GetComponent<ImageAnimation>();
    fishImage = GetComponent<Image>();
    boxCollider = GetComponent<BoxCollider2D>();

    // Collider & hit point
    boxCollider.enabled = true;
    boxCollider.size = data.colliderSize;
    boxCollider.offset = data.colliderOffset;

    HitPoint.localPosition = new Vector3(data.colliderOffset.x, data.colliderOffset.y, 0);

    // Animation
    imageAnimation.SetAnimationData(data.animationFrames, data.animationSpeed, data.loop);
    imageAnimation.StartAnimation();

    Rect.sizeDelta = data.spriteSize;

    // Fade in
    fishImage.color = new Color(1, 1, 1, 0);
    fishImage.DOFade(1f, 0.2f).SetUpdate(true);

    // HARD lifespan guarantee
    StartLifeTimeout(data.duration);
  }

  protected void SetupFallbackMovement()
  {
    bool rtl = UnityEngine.Random.value > 0.5f;
    FlipSprite(faceRight: !rtl);

    var splines =
      CurvyPathProvider.Instance.GetFallbackSplines(rtl);

    CurvySpline spline = splines[UnityEngine.Random.Range(0, splines.Count)];

    ApplySpline(spline, rtl);
  }

  protected void ApplySpline(CurvySpline spline, bool rtl)
  {
    splineController.Stop();
    splineController.Spline = spline;
    splineController.Position = 0;
    splineController.Speed = 0;

    splineController.MoveMode = CurvyController.MoveModeEnum.AbsolutePrecise;
    splineController.OrientationMode = OrientationModeEnum.Tangent;
    splineController.Clamping = CurvyClamping.Clamp;
    splineController.PlayAutomatically = true;
    splineController.OrientationAxis = rtl ? OrientationAxisEnum.Left : OrientationAxisEnum.Right;

    float visibleTimeSec = data.duration / 1000f;
    baseSpeed = spline.Length / visibleTimeSec;
    ApplySpeed();

    splineController.OnEndReached.RemoveListener(OnPathComplete);
    splineController.OnEndReached.AddListener(OnPathComplete);

    splineController.Play();
  }

  protected void ApplySpeed()
  {
    if (splineController != null)
      splineController.Speed = baseSpeed * speedMultiplier;
  }

  protected void FlipSprite(bool faceRight)
  {
    Vector3 scale = transform.localScale;
    scale.x = Mathf.Abs(scale.x) * (faceRight ? -1 : 1);
    transform.localScale = scale;
  }

  internal void SetSpeedMultiplier(float multiplier)
  {
    speedMultiplier = multiplier;
    ApplySpeed();
  }

  private void OnPathComplete(CurvySplineMoveEventArgs args)
  {
    DespawnFish();
  }

  // --------------------------------------------------------
  // DESPAWN LOGIC (IDEMPOTENT)
  // --------------------------------------------------------
  protected void DespawnFish()
  {
    if (isDespawning)
      return;

    isDespawning = true;

    movementTween?.Kill();
    damageTween?.Kill();
    DOTween.Kill(fishImage);

    // Visual fade is optional, logic must NOT depend on it
    fishImage.DOFade(0f, 0.15f)
             .SetUpdate(true)
             .OnComplete(FinalizeDespawn);
  }

  private void ForceDespawn()
  {
    if (isDespawning)
      return;

    isDespawning = true;
    FinalizeDespawn();
  }

  private void FinalizeDespawn()
  {
    if (lifeTimeoutRoutine != null)
    {
      StopCoroutine(lifeTimeoutRoutine);
      lifeTimeoutRoutine = null;
    }

    FishManager.Instance.DespawnFish(this);
    ResetFish();
  }

  // --------------------------------------------------------
  // LIFETIME WATCHDOG (AUTHORITATIVE)
  // --------------------------------------------------------
  private void StartLifeTimeout(int durationMs)
  {
    if (lifeTimeoutRoutine != null)
      StopCoroutine(lifeTimeoutRoutine);

    lifeTimeoutRoutine = StartCoroutine(LifeTimeoutRoutine(durationMs));
  }

  private IEnumerator LifeTimeoutRoutine(int durationMs)
  {
    yield return new WaitForSecondsRealtime(durationMs / 1000f + 1.5f);

    if (!isDespawning)
    {
      Debug.LogWarning($"[Fish Timeout] Forced despawn: {data.variant} {data.fishType}");
      ForceDespawn();
    }
  }

  // --------------------------------------------------------
  // RESET (POOL SAFE)
  // --------------------------------------------------------
  internal virtual void ResetFish()
  {
    data = null;
    movementTween?.Kill();
    damageTween?.Kill();

    isDespawning = false;

    if (fishImage != null)
      fishImage.color = Color.white;

    transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    transform.localScale = Vector3.one;

    if (splineController != null)
    {
      splineController.Stop();
      splineController.Position = 0;
      splineController.Speed = 0;
    }

    speedMultiplier = 1f;
  }

  // --------------------------------------------------------
  // OPTIONAL EDITOR-ONLY SAFETY WARNING
  // --------------------------------------------------------
#if UNITY_EDITOR
  private void LateUpdate()
  {
    if (!isDespawning &&
        data != null &&
        Time.realtimeSinceStartup - spawnTime >
        (data.duration / 1000f) + 2f)
    {
      Debug.LogWarning($"[Fish Overstayed] {data.variant} {data.fishType}");
    }
  }
#endif

  internal void PlayLaserImpact() { }
  internal void StopLaserImpact() { }
}
