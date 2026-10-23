using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using DG.Tweening;

internal enum FishState
{
  Pooled,  // in the pool, not on screen
  Alive,   // swimming and hittable
  Dying,   // server confirmed a kill; playing the death animation
  Leaving  // left the screen / expired; fading out while still swimming
}

// Fish lifecycle:  Pooled -> Alive -> (Dying | Leaving) -> Release() -> Pooled
//
// - Kill()  : the server said the fish is dead. Only from Alive.
// - Leave() : the fish expired or swam off. Only from Alive.
// - Release(): the single, idempotent exit back to the pool. Death/leave tweens call it from
//   OnKill, which DOTween also fires on completion, so it runs even if a tween is killed early.
//   A watchdog in Update releases fish whose death animation never reported back.
//
// Server-driven fish (ML-FG) position themselves every frame from ServerClock.NowMs and their
// ServerPath. Old-backend Curvy movement and torpedo bookkeeping live in BaseFish.Legacy.cs.
internal partial class BaseFish : MonoBehaviour
{
  private const double HeadingLookAhead = 0.01; // matches the reference client's heading sample

  [SerializeField] internal FishData data;
  [FormerlySerializedAs("torpedoViewportPadding")]
  [SerializeField] private float viewportPadding = 0.0005f;

  [Header("Server Movement")]
  [SerializeField] private float turnSpeed = 540f;      // deg/s, smooths heading changes at path corners
  [SerializeField] private float flipThreshold = 0.15f; // |dir.x| needed to flip facing, avoids flapping on vertical runs

  [Header("Despawn")]
  [SerializeField] private float leaveFadeDuration = 0.25f;
  [SerializeField] private float deathFadeDuration = 0.15f;
  [SerializeField] private float maxDeathDuration = 8f; // watchdog for death animations that never complete

  internal FishState State { get; private set; } = FishState.Pooled;
  internal string Id => data?.fishId;
  internal ServerPath Path { get; private set; }
  internal bool IsServerDriven => Path != null;
  internal float HitboxRadius { get; private set; }
  internal bool IsVisibleInViewport { get; private set; }

  internal Transform HitPoint => transform.GetChild(0);
  internal RectTransform Rect => GetComponent<RectTransform>();
  internal Vector3 ColliderMidPoint =>
    boxCollider != null ? transform.TransformPoint(boxCollider.offset) : transform.position;

  protected ImageAnimation imageAnimation;
  protected Image fishImage;
  protected BoxCollider2D boxCollider;
  protected Tween damageTween;
  protected bool movementFrozen;

  private Tween lifecycleTween;
  private Action released;
  private float baseAnimationSpeed;
  private int aliveSinceFrame;
  private float exitStartedAt;
  private bool facingRight;
  private bool hasPose;

  // --------------------------------------------------------
  // SPAWN
  // --------------------------------------------------------

  // Server-driven spawn (ML-FG backend). The fish is placed on its path before its first frame.
  internal void InitializeFromServer(FishData visual, ServerPath path, float hitboxRadius)
  {
    Path = path;
    HitboxRadius = hitboxRadius > 0f ? hitboxRadius : 40f;
    BeginLife(visual);
    DisableLegacyMovement();
    UpdateServerMovement();
    OnServerSpawned();
  }

  protected virtual void OnServerSpawned() { }

  private void BeginLife(FishData visual)
  {
    data = visual;
    released = null;
    State = FishState.Alive;
    aliveSinceFrame = Time.frameCount;
    movementFrozen = false;
    hasPose = false;
    IsVisibleInViewport = false;
    CacheComponents();
    ResetLegacyState();
    SetupVisuals();
  }

  private void CacheComponents()
  {
    imageAnimation = GetComponent<ImageAnimation>();
    fishImage = GetComponent<Image>();
    boxCollider = GetComponent<BoxCollider2D>();
    CacheLegacyComponents();
  }

  private void SetupVisuals()
  {
    // The box collider only serves pointer picking and target lock; hits are tested in server space.
    boxCollider.enabled = true;
    boxCollider.size = data.colliderSize;
    boxCollider.offset = data.colliderOffset;
    HitPoint.localPosition = new Vector3(data.colliderOffset.x, data.colliderOffset.y, 0);

    imageAnimation.StopAnimation();
    imageAnimation.SetAnimationData(data.animationFrames, data.animationSpeed, data.loop);
    imageAnimation.StartAnimation();
    baseAnimationSpeed = data.animationSpeed;

    Rect.sizeDelta = data.spriteSize;

    if (UIManager.Instance.activeGun == UIManager.GunType.Torpedo && !UIManager.Instance.IsValidTorpedoTarget(this))
    {
      SetAlpha(0.25f);
    }
    else
    {
      fishImage.color = new Color(1, 1, 1, 0);
      fishImage.DOFade(1f, 0.2f).SetUpdate(true);
    }
  }

  // --------------------------------------------------------
  // PER FRAME
  // --------------------------------------------------------

  private void Update()
  {
    if (State == FishState.Pooled)
      return;

    if (IsServerDriven && !movementFrozen && (State == FishState.Alive || State == FishState.Leaving))
      UpdateServerMovement();

    if ((State == FishState.Dying || State == FishState.Leaving) &&
        Time.unscaledTime - exitStartedAt > maxDeathDuration)
    {
      GameLog.Warn(LogCategory.Fish, $"Exit watchdog released fish | variant={data?.variant} | id={Id} | state={State}");
      Release();
      return;
    }

    UpdateViewportVisibility();
    OnCustomUpdate();
  }

  protected virtual void OnCustomUpdate() { }

  private void UpdateServerMovement()
  {
    double progress = Path.ProgressAt(ServerClock.NowMs);
    Vector2 pos = Path.PositionAt(progress);
    Vector2 ahead = Path.PositionAt(Math.Min(progress + HeadingLookAhead, 1.0));

    transform.position = Playfield.ServerToWorld(pos);

    Vector2 dir = ahead - pos;
    if (dir.sqrMagnitude > 0.0001f)
      ApplyHeading(Playfield.ServerDirToLocal(dir.normalized));

    if (State == FishState.Alive && progress >= 1.0)
      Leave();
  }

  // Sprites face left natively (see FlipSprite). Facing flips with hysteresis; rotation is
  // smoothed so the polyline's corners don't snap the fish.
  private void ApplyHeading(Vector2 localDir)
  {
    bool wantRight = facingRight;
    if (!hasPose)
      wantRight = localDir.x >= 0f;
    else if (localDir.x > flipThreshold)
      wantRight = true;
    else if (localDir.x < -flipThreshold)
      wantRight = false;

    float angle = Mathf.Atan2(localDir.y, localDir.x) * Mathf.Rad2Deg;
    if (!wantRight)
      angle -= 180f;

    Quaternion target = Quaternion.Euler(0f, 0f, angle);
    bool snap = !hasPose || wantRight != facingRight;

    facingRight = wantRight;
    FlipSprite(faceRight: wantRight);
    transform.localRotation = snap
      ? target
      : Quaternion.RotateTowards(transform.localRotation, target, turnSpeed * Time.unscaledDeltaTime);
    hasPose = true;
  }

  // --------------------------------------------------------
  // HIT TESTING (server space, same rule as the server)
  // --------------------------------------------------------

  internal bool IsHittable(double nowMs) =>
    State == FishState.Alive && IsServerDriven && !movementFrozen && !Path.IsBeforeSpawn(nowMs);

  internal Vector2 ServerPositionAt(double nowMs) => Path.PositionAtTime(nowMs);

  // --------------------------------------------------------
  // EXITS
  // --------------------------------------------------------

  internal void Kill()
  {
    if (State != FishState.Alive)
      return;

    State = FishState.Dying;
    exitStartedAt = Time.unscaledTime;
    movementFrozen = true;
    SetPickable(false);
    fishImage.DOKill();
    FishManager.Instance?.MoveToAnimParent(this);
    PlayDeath(Release);
  }

  internal void Leave()
  {
    if (State != FishState.Alive)
      return;

    State = FishState.Leaving;
    exitStartedAt = Time.unscaledTime;
    SetPickable(false);
    fishImage.DOKill();
    BindLifecycle(fishImage.DOFade(0f, leaveFadeDuration), Release);
  }

  // Must call onComplete exactly once when the death visuals are done (Release is idempotent,
  // so calling it twice is harmless; never calling it is caught by the watchdog).
  protected virtual void PlayDeath(Action onComplete)
  {
    BindLifecycle(fishImage.DOFade(0f, deathFadeDuration), onComplete);
  }

  protected void BindLifecycle(Tween tween, Action onComplete)
  {
    lifecycleTween = tween.SetUpdate(true).OnKill(() => onComplete?.Invoke());
  }

  internal void AddReleaseCallback(Action callback)
  {
    released += callback;
  }

  internal void Release()
  {
    if (State == FishState.Pooled)
      return;
    State = FishState.Pooled;

    Action callbacks = released;
    released = null;
    callbacks?.Invoke(); // still positioned here, e.g. coin blast at the final spot

    OnReleased();
    FishManager.Instance?.OnFishReleased(this);
    ResetFish();
  }

  protected virtual void OnReleased() { }

  // --------------------------------------------------------
  // RESET (POOL SAFE)
  // --------------------------------------------------------

  internal virtual void ResetFish()
  {
    released = null;
    StopAllCoroutines();
    CancelInvoke();

    lifecycleTween?.Kill();
    lifecycleTween = null;
    damageTween?.Kill();
    DOTween.Kill(transform);
    if (fishImage != null)
    {
      fishImage.DOKill();
      fishImage.color = Color.white;
    }

    if (imageAnimation != null && baseAnimationSpeed > 0f)
      imageAnimation.SetAnimationSpeed(baseAnimationSpeed);

    ResetLegacyState();
    FishManager.Instance?.RestoreFromAnimParent(this);

    transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    transform.localScale = Vector3.one;

    data = null;
    Path = null;
    HitboxRadius = 0f;
    IsVisibleInViewport = false;
    movementFrozen = false;
    hasPose = false;
  }

  // --------------------------------------------------------
  // VISUAL HELPERS
  // --------------------------------------------------------

  protected void FlipSprite(bool faceRight)
  {
    Vector3 scale = transform.localScale;
    scale.x = Mathf.Abs(scale.x) * (faceRight ? -1 : 1);
    transform.localScale = scale;
  }

  private void SetPickable(bool pickable)
  {
    if (boxCollider != null)
      boxCollider.enabled = pickable;
  }

  internal virtual IEnumerator DamageAnimation(Color damageColor)
  {
    if (fishImage == null)
    {
      GameLog.Error(LogCategory.Fish, "fishImage not found for " + data?.variant);
      yield break;
    }

    if (damageTween != null && damageTween.IsPlaying())
      yield return damageTween.WaitForCompletion();

    if (State != FishState.Alive)
      yield break;

    damageTween?.Kill();
    damageTween = DOTween.Sequence()
      .Append(fishImage.DOColor(damageColor, 0.06f).SetEase(Ease.OutQuad))
      .AppendInterval(0.08f)
      .Append(fishImage.DOColor(Color.white, 0.08f).SetEase(Ease.OutQuad));
  }

  internal void SetAlpha(float alpha)
  {
    if (fishImage == null)
      return;

    Color c = fishImage.color;
    c.a = alpha;
    fishImage.color = c;
  }

  internal void SetAnimationSpeedMultiplier(float multiplier)
  {
    if (imageAnimation == null)
      return;

    float speed = baseAnimationSpeed > 0f ? baseAnimationSpeed : imageAnimation.AnimationSpeed;
    imageAnimation.SetAnimationSpeed(speed * Mathf.Max(0.01f, multiplier));
  }

  // Any part of the collider overlapping the viewport. Skips the first two frames so a
  // freshly pooled fish isn't reported at its pre-spawn position.
  private void UpdateViewportVisibility()
  {
    IsVisibleInViewport = false;

    if (State != FishState.Alive || PendingVisualDeath || Time.frameCount - aliveSinceFrame < 2)
      return;

    var cam = Camera.main;
    if (cam == null || boxCollider == null)
      return;

    var bounds = boxCollider.bounds;
    Vector3 min = cam.WorldToViewportPoint(bounds.min);
    Vector3 max = cam.WorldToViewportPoint(bounds.max);
    if (max.z <= 0)
      return;

    IsVisibleInViewport =
      max.x >= viewportPadding &&
      min.x <= 1f - viewportPadding &&
      max.y >= viewportPadding &&
      min.y <= 1f - viewportPadding;
  }
}
