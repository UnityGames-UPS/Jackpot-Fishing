using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using FluffyUnderware.Curvy;
using FluffyUnderware.Curvy.Controllers;

// PAUSED FEATURES (old backend). Client-picked Curvy paths, torpedo bookkeeping, and the old
// death/despawn API used by EffectFish, TorpedoGun, LazerGun, TorpedoBulletView and the old
// hit-result handler. Kept compiling on top of the new lifecycle in BaseFish.cs so those
// features can be re-enabled once the ML-FG backend supports them. The server-driven
// normal-bullet flow does not use anything in this file.
internal partial class BaseFish
{
  internal enum DeathCause
  {
    None,
    Torpedo,
    Bullet,
    Lazer,
    ServerCleanup
  }

  internal DeathCause deathCause = DeathCause.None;
  internal bool KillOnTorpedoArrival;
  internal bool PendingVisualDeath;
  internal bool WaitingForKillingTorpedo;
  internal int ActiveTorpedoCount { get; private set; }

  protected SplineController splineController;
  protected Tween movementTween;
  protected float baseSpeed;
  protected float speedMultiplier = 1f;

  private CurvySpline reservedSpline;
  private readonly List<Action> onNextTorpedoImpact = new List<Action>();
  private Coroutine killFailSafeRoutine;

  // Old flag names, now derived from State.
  internal bool isDespawning => State != FishState.Alive;
  internal bool finalized => State == FishState.Pooled;

  // Old name for the release callback. Assigning replaces any callbacks already registered.
  internal Action OnFishDespawned
  {
    get => released;
    set => released = value;
  }

  // Old-backend spawn: visuals only; subclasses add Curvy movement via SetupFallbackMovement.
  internal virtual void Initialize(FishData data)
  {
    Path = null;
    HitboxRadius = 0f;
    BeginLife(data);
  }

  private void CacheLegacyComponents()
  {
    splineController = GetComponent<SplineController>();
  }

  private void DisableLegacyMovement()
  {
    if (splineController == null)
      return;

    splineController.Stop();
    splineController.enabled = false;
  }

  private void ResetLegacyState()
  {
    KillOnTorpedoArrival = false;
    PendingVisualDeath = false;
    WaitingForKillingTorpedo = false;
    deathCause = DeathCause.None;
    ActiveTorpedoCount = 0;
    onNextTorpedoImpact.Clear();
    killFailSafeRoutine = null;

    movementTween?.Kill();
    speedMultiplier = 1f;

    if (reservedSpline != null)
    {
      CurvyPathProvider.Instance?.ReleaseSpline(reservedSpline);
      reservedSpline = null;
    }

    if (splineController != null)
    {
      splineController.enabled = true;
      splineController.Stop();
      splineController.Position = 0;
      splineController.Speed = 0;
    }
  }

  // Old bullets (physics triggers). Server-driven fish are hit-tested in server space instead.
  private void OnTriggerEnter2D(Collider2D other)
  {
    if (IsServerDriven || State != FishState.Alive || PendingVisualDeath)
      return;

    if (!other.TryGetComponent<BulletView>(out var bullet))
      return;

    bullet.OnFishHit(this);
    SocketIOManager.Instance.SendHitEvent(data?.fishId, "normal", variant: data?.variant);
  }

  // --------------------------------------------------------
  // CURVY MOVEMENT
  // --------------------------------------------------------

  protected void SetupFallbackMovement()
  {
    bool rtl = UnityEngine.Random.value > 0.5f;
    FlipSprite(faceRight: !rtl);

    var provider = CurvyPathProvider.Instance;
    var splines = data != null && data.fishType == FishType.Jackpot_Dragon
      ? provider.GetJackpotDragonSplines(rtl)
      : provider.GetFallbackSplines(rtl);
    if (splines == null || splines.Count == 0)
      return;

    ApplySpline(provider.ReserveUniqueSpline(splines), rtl);
  }

  protected void ApplySpline(CurvySpline spline, bool rtl)
  {
    if (spline == null)
    {
      GameLog.Warn(LogCategory.Legacy, "ApplySpline called with null spline");
      return;
    }

    if (reservedSpline != null && reservedSpline != spline)
      CurvyPathProvider.Instance?.ReleaseSpline(reservedSpline);

    reservedSpline = spline;
    CurvyPathProvider.Instance?.ReserveSpline(spline);

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

  // Curvy fish only. Server-driven fish speed is owned by the server (motionKeyframes).
  internal void SetSpeedMultiplier(float multiplier)
  {
    speedMultiplier = multiplier;
    ApplySpeed();
  }

  private void OnPathComplete(CurvySplineMoveEventArgs args)
  {
    Leave();
  }

  internal void StopPathMovement()
  {
    SetPickable(false);
    movementFrozen = true;
    movementTween?.Kill();

    if (splineController != null)
    {
      splineController.PlayAutomatically = false;
      splineController.Pause();
      splineController.enabled = false;
      splineController.Speed = 0f;
    }
  }

  // --------------------------------------------------------
  // OLD DEATH API
  // --------------------------------------------------------

  internal virtual void Die()
  {
    if (KillOnTorpedoArrival || WaitingForKillingTorpedo)
      return;

    PendingVisualDeath = false;
    Kill();
  }

  // Fade out and release, skipping the death animation.
  internal void ForceDespawn()
  {
    if (State != FishState.Alive)
      return;

    State = FishState.Leaving;
    exitStartedAt = Time.unscaledTime;
    movementFrozen = true;
    SetPickable(false);
    fishImage.DOKill();
    FishManager.Instance?.MoveToAnimParent(this);
    BindLifecycle(fishImage.DOFade(0f, deathFadeDuration), Release);
  }

  internal void MarkPendingDeath()
  {
    PendingVisualDeath = true;
  }

  // --------------------------------------------------------
  // TORPEDO BOOKKEEPING
  // --------------------------------------------------------

  internal virtual void OnTorpedoImpact()
  {
    if (onNextTorpedoImpact.Count == 0)
      return;

    var callbacks = onNextTorpedoImpact.ToArray();
    onNextTorpedoImpact.Clear();
    for (int i = 0; i < callbacks.Length; i++)
      callbacks[i]?.Invoke();
  }

  internal void RegisterIncomingTorpedo()
  {
    ActiveTorpedoCount++;
  }

  internal void UnregisterIncomingTorpedo()
  {
    if (ActiveTorpedoCount > 0)
      ActiveTorpedoCount--;
  }

  internal void WaitForLastTorpedo(Action onReady, float timeout)
  {
    if (onReady == null)
      return;

    if (ActiveTorpedoCount <= 0)
    {
      onReady.Invoke();
      return;
    }

    onNextTorpedoImpact.Add(onReady);
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
      Die();
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
    Die();
  }

  private void LogDeath(string source)
  {
    if (!GameLog.IsOn(LogCategory.Legacy))
      return;

    GameLog.Info(LogCategory.Legacy,
      $"☠️ FISH DEATH | source={source} | variant={data?.variant} | id={data?.fishId} | " +
      $"PendingVisualDeath={PendingVisualDeath} | WaitingForTorpedo={WaitingForKillingTorpedo} | " +
      $"KillOnArrival={KillOnTorpedoArrival} | deathCause={deathCause}"
    );
  }

  // --------------------------------------------------------
  // TORPEDO / LASER AIMING
  // --------------------------------------------------------

  internal void PlayLaserImpact() { }
  internal void StopLaserImpact() { }

  internal Vector3 GetAimPoint(Camera cam)
  {
    if (data == null || data.fishType != FishType.Jackpot_Dragon)
      return ColliderMidPoint;

    if (boxCollider == null)
      boxCollider = GetComponent<BoxCollider2D>();
    if (boxCollider == null)
      return HitPoint.position;

    Bounds bounds = boxCollider.bounds;
    Vector3 center = bounds.center;
    const float inwardOffset = 1f;
    float halfWidth = Mathf.Abs(boxCollider.size.x * transform.lossyScale.x) * 0.5f;
    float edgeOffset = Mathf.Max(halfWidth - inwardOffset, 0f);
    Vector3 right = center + transform.right * edgeOffset;
    Vector3 left = center - transform.right * edgeOffset;

    if (cam == null)
      return center;

    if (IsPointVisible(cam, center, 0f))
      return center;
    if (IsPointVisible(cam, left, 0f))
      return left;
    if (IsPointVisible(cam, right, 0f))
      return right;

    return center;
  }

  private bool IsPointVisible(Camera cam, Vector3 worldPoint, float padding)
  {
    Vector3 vp = cam.WorldToViewportPoint(worldPoint);
    return vp.z > 0f &&
           vp.x >= padding && vp.x <= 1f - padding &&
           vp.y >= padding && vp.y <= 1f - padding;
  }
}
