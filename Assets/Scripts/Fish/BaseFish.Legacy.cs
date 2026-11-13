using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

// PAUSED FEATURES (old backend). Torpedo bookkeeping, and the old
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

  // Old-backend spawn: visuals only. The old client-picked Curvy paths are gone, so a fish
  // spawned this way does not move; movement comes from a ServerPath (InitializeFromServer).
  internal virtual void Initialize(FishData data)
  {
    Path = null;
    SetSegments(0f, null); // default circle so the fish stays pickable
    BeginLife(data);
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

  internal void StopPathMovement()
  {
    SetPickable(false);
    movementFrozen = true;
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
}
