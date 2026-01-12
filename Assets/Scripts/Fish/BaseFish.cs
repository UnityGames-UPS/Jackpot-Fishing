using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using FluffyUnderware.Curvy;
using FluffyUnderware.Curvy.Controllers;
using System.Collections;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(SplineController))]
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(ImageAnimation))]
[RequireComponent(typeof(BoxCollider2D))]
internal class BaseFish : MonoBehaviour
{
  [SerializeField] internal FishData data;
  internal bool KillOnTorpedoArrival;
  internal Transform HitPoint => transform.GetChild(0);
  internal Vector3 ColliderMidPoint
  {
    get
    {
      if (boxCollider == null)
        boxCollider = GetComponent<BoxCollider2D>();
      return boxCollider.bounds.center;
    }
  }
  internal RectTransform Rect => GetComponent<RectTransform>();
  internal Action OnFishDespawned;
  protected Tween movementTween;
  protected Tween damageTween;
  protected ImageAnimation imageAnimation;
  protected Image fishImage;
  protected BoxCollider2D boxCollider;
  protected SplineController splineController;
  protected float baseSpeed;
  protected float speedMultiplier = 1f;
  [SerializeField] private float torpedoViewportPadding = 0.05f;
  private Coroutine lifeTimeoutRoutine;
  private Coroutine despawnFinalizeRoutine;
  internal bool isDespawning;
  internal bool PendingVisualDeath;
  private bool finalized;
  internal bool TorpedoTargetVisible;

  internal enum DeathCause
  {
    None,
    Torpedo,
    Bullet,
    Laser,
    ServerCleanup
  }

  internal DeathCause deathCause = DeathCause.None;
  internal bool WaitingForKillingTorpedo;
  private Coroutine killFailSafeRoutine;


  internal virtual void Initialize(FishData data)
  {
    this.data = data;
    OnFishDespawned = null;

    finalized = false;
    TorpedoTargetVisible = false;
    isDespawning = false;
    PendingVisualDeath = false;
    KillOnTorpedoArrival = false;

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
    if(UIManager.Instance.activeGun == UIManager.GunType.Torpedo)
    {
      if (!UIManager.Instance.IsValidTorpedoTarget(this))
      {
        SetAlpha(0.25f);
      }
    }
    else
    {
      fishImage.color = new Color(1, 1, 1, 0);
      fishImage.DOFade(1f, 0.2f).SetUpdate(true);
    }

  }

  private void Update()
  {
    UpdateTorpedoVisibility();
  }

  private void OnTriggerEnter2D(Collider2D other)
  {
    if (isDespawning || PendingVisualDeath || !gameObject.activeInHierarchy)
      return;

    if (!other.TryGetComponent<BulletView>(out var bullet))
      return;

    bullet.OnFishHit(this);
    if (data == null || string.IsNullOrEmpty(data.fishId))
    {
      Debug.LogWarning($"[Fish] Bullet hit missing fishId | variant={data?.variant}");
      return;
    }

    SocketIOManager.Instance.SendHitEvent(data.fishId, "normal", variant: data.variant);
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

  internal virtual IEnumerator DamageAnimation(Color damageColor)
  {
    if (fishImage == null)
    {
      Debug.LogError("fishImage not found for " + data.variant);
      yield break;
    }

    if (damageTween != null && damageTween.IsPlaying())
      yield return damageTween.WaitForCompletion();

    damageTween?.Kill();

    damageTween = DOTween.Sequence()
        .Append(fishImage.DOColor(damageColor, 0.06f).SetEase(Ease.OutQuad))
        .AppendInterval(0.08f).SetEase(Ease.OutQuad)
        .Append(fishImage.DOColor(Color.white, 0.08f).SetEase(Ease.OutQuad));
  }

  internal virtual void Die(bool despawnAtEnd = true)
  {
    if (isDespawning)
      return;

    if (KillOnTorpedoArrival)
      return;

    PendingVisualDeath = false;

    if (WaitingForKillingTorpedo)
      return;

    boxCollider.enabled = false;
    if (splineController != null)
    {
      splineController.PlayAutomatically = false;
      splineController.Pause();
    }

    if (despawnAtEnd)
      DespawnFish();
  }

  // --------------------------------------------------------
  // DESPAWN LOGIC (IDEMPOTENT)
  // --------------------------------------------------------
  protected void DespawnFish()
  {
    if (isDespawning)
      return;

    if (!gameObject.activeInHierarchy)
    {
      FinalizeDespawn();
      return;
    }

    if (PendingVisualDeath)
    {
      Debug.LogWarning(
        $"[Fish] Despawn while pending | " +
        $"variant={data?.variant} | id={data?.fishId}"
      );
    }

    isDespawning = true;

    // Debug.Log("Despawning fish : " + data?.variant + " " + data?.fishId);

    movementTween?.Kill();
    damageTween?.Kill();

    DOTween.Kill(transform);
    DOTween.Kill(fishImage);

    // Visual fade is optional, logic must NOT depend on it
    fishImage.DOFade(0f, 0.15f)
             .SetUpdate(true)
             .OnComplete(FinalizeDespawn);

    if (despawnFinalizeRoutine == null && gameObject.activeInHierarchy)
      despawnFinalizeRoutine = StartCoroutine(DespawnFinalizeFallback(0.5f));
  }

  private void FinalizeDespawn()
  {
    if (finalized)
      return;
    finalized = true;
    if (lifeTimeoutRoutine != null)
    {
      StopCoroutine(lifeTimeoutRoutine);
      lifeTimeoutRoutine = null;
    }
    if (killFailSafeRoutine != null)
    {
      StopCoroutine(killFailSafeRoutine);
      killFailSafeRoutine = null;
    }
    if (despawnFinalizeRoutine != null)
    {
      StopCoroutine(despawnFinalizeRoutine);
      despawnFinalizeRoutine = null;
    }

    FishManager.Instance.DespawnFish(this);
    OnFishDespawned?.Invoke();
    ResetFish();
  }

  private bool IsFishStillValid()
  {
    return data != null && data.fishId != null && !isDespawning;
  }

  // --------------------------------------------------------
  // RESET (POOL SAFE)
  // --------------------------------------------------------
  internal virtual void ResetFish()
  {
    OnFishDespawned = null;
    data = null;
    movementTween?.Kill();
    damageTween?.Kill();

    isDespawning = false;
    PendingVisualDeath = false;
    KillOnTorpedoArrival = false;

    WaitingForKillingTorpedo = false;
    deathCause = DeathCause.None;
    TorpedoTargetVisible = false;
    if (killFailSafeRoutine != null)
    {
      StopCoroutine(killFailSafeRoutine);
      killFailSafeRoutine = null;
    }
    if (despawnFinalizeRoutine != null)
    {
      StopCoroutine(despawnFinalizeRoutine);
      despawnFinalizeRoutine = null;
    }

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

  private void UpdateTorpedoVisibility()
  {
    if (isDespawning || PendingVisualDeath || !gameObject.activeInHierarchy)
    {
      TorpedoTargetVisible = false;
      return;
    }

    var cam = Camera.main;
    if (cam == null)
    {
      TorpedoTargetVisible = false;
      return;
    }

    if (boxCollider == null)
      boxCollider = GetComponent<BoxCollider2D>();

    if (boxCollider == null)
    {
      TorpedoTargetVisible = false;
      return;
    }

    var bounds = boxCollider.bounds;
    Vector3 min = cam.WorldToViewportPoint(bounds.min);
    Vector3 max = cam.WorldToViewportPoint(bounds.max);

    if (max.z <= 0)
    {
      TorpedoTargetVisible = false;
      return;
    }

    TorpedoTargetVisible =
      min.x > torpedoViewportPadding &&
      max.x < 1f - torpedoViewportPadding &&
      min.y > torpedoViewportPadding &&
      max.y < 1f - torpedoViewportPadding;
  }
  internal void SetAlpha(float alpha)
  {
    if (fishImage == null)
      return;

    Color c = fishImage.color;
    c.a = alpha;
    fishImage.color = c;
  }
  internal void MarkPendingDeath()
  {
    PendingVisualDeath = true;
    if (boxCollider != null)
      boxCollider.enabled = false;
  }
  internal void WaitForTorpedoKill(float timeout = 10f)
  {
    WaitingForKillingTorpedo = true;

    if (killFailSafeRoutine != null)
      StopCoroutine(killFailSafeRoutine);

    killFailSafeRoutine = StartCoroutine(KillFailSafe(timeout));
  }

  private IEnumerator KillFailSafe(float t)
  {
    yield return new WaitForSecondsRealtime(t);

    if (WaitingForKillingTorpedo)
    {
      LogDeath("TORPEDO_FAILSAFE");

      WaitingForKillingTorpedo = false;
      KillOnTorpedoArrival = false;
      Die(true);
    }
  }

  internal void OnKillingTorpedoArrived()
  {
    LogDeath("TORPEDO_ARRIVAL");

    if (!WaitingForKillingTorpedo)
      return;

    WaitingForKillingTorpedo = false;

    if (killFailSafeRoutine != null)
    {
      StopCoroutine(killFailSafeRoutine);
      killFailSafeRoutine = null;
    }
    KillOnTorpedoArrival = false;
    Die(true);
  }

  private IEnumerator DespawnFinalizeFallback(float t)
  {
    yield return new WaitForSecondsRealtime(t);

    despawnFinalizeRoutine = null;

    if (finalized)
      yield break;

    Debug.LogWarning(
      $"[Fish] Despawn finalize fallback | " +
      $"variant={data?.variant} | id={data?.fishId}"
    );
    FinalizeDespawn();
  }

  private void LogDeath(string source)
  {
    Debug.Log(
      $"☠️ FISH DEATH | " +
      $"source={source} | " +
      $"variant={data?.variant} | " +
      $"id={data?.fishId} | " +
      $"PendingVisualDeath={PendingVisualDeath} | " +
      $"WaitingForTorpedo={WaitingForKillingTorpedo} | " +
      $"KillOnArrival={KillOnTorpedoArrival} | " +
      $"deathCause={deathCause}"
    );
  }


  internal void PlayLaserImpact() { }
  internal void StopLaserImpact() { }
}
