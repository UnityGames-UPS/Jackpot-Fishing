using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using DG.Tweening;

// A server hit circle in server px (point-in-circle, same test as the backend).
internal struct HitCircle
{
  internal Vector2 center;
  internal float radius;
}

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
// ServerPath. Old-backend torpedo bookkeeping lives in BaseFish.Legacy.cs.
internal partial class BaseFish : MonoBehaviour
{
  private const double HeadingLookAhead = 0.01; // matches the reference client's heading sample

  [SerializeField] internal FishData data;

  // Wire these on the prefab. Missing ones are filled from this object in Awake/OnValidate.
  [Header("References")]
  [SerializeField] protected Image fishImage;
  [SerializeField] protected ImageAnimation imageAnimation;
  [SerializeField] private RectTransform rect;
  [Tooltip("Hit circles; the dragon needs one per segment. Missing ones are added at spawn.")]
  [SerializeField] private List<CircleCollider2D> hitColliders = new List<CircleCollider2D>();
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
  internal float HitboxRadius { get; private set; } // head radius, server px
  // A hit circle overlaps the 1920x1080 playfield (server space, same as the server and bullets).
  internal bool IsVisibleInViewport { get; private set; }

  internal RectTransform Rect => rect;

  // Center of the hit circles as last placed (the pivot for single-circle fish). Uses the
  // colliders, not the path, so it stays put once the fish is frozen by a kill.
  internal Vector3 ColliderMidPoint
  {
    get
    {
      if (activeColliderCount <= 1)
        return transform.position;
      Vector3 sum = Vector3.zero;
      for (int i = 0; i < activeColliderCount; i++)
        sum += transform.TransformPoint(hitColliders[i].offset);
      return sum / activeColliderCount;
    }
  }

  protected Tween damageTween;
  protected bool movementFrozen;

  private Tween lifecycleTween;
  private Action released;
  private float baseAnimationSpeed;
  private int aliveSinceFrame;
  private float exitStartedAt;
  private bool facingRight;
  private bool hasPose;

  // Hitboxes come from the spawn (hitboxRadius, or segments for the dragon). One
  // CircleCollider2D per circle, used for pointer picking and target lock; bullets test
  // the same circles in server space via GetHitCircles.
  private const float DefaultHitboxRadius = 40f; // mock client's fallback
  private static readonly List<HitCircle> circleScratch = new List<HitCircle>(8);
  private HitSegment[] segments;
  private int activeColliderCount;

  protected virtual void Awake()
  {
    ResolveReferences();
    // The old hand-sized box doesn't match the server hitbox; keep it out of picking.
    if (TryGetComponent<BoxCollider2D>(out var legacyBox))
      legacyBox.enabled = false;
  }

#if UNITY_EDITOR
  protected virtual void OnValidate() => ResolveReferences();
#endif

  // Fallbacks for references not wired on the prefab (null-checked, never per spawn).
  private void ResolveReferences()
  {
    if (fishImage == null)
      fishImage = GetComponent<Image>();
    if (imageAnimation == null)
      imageAnimation = GetComponent<ImageAnimation>();
    if (rect == null)
      rect = GetComponent<RectTransform>();
    if (hitColliders.Count == 0)
      GetComponents(hitColliders);
  }

  // --------------------------------------------------------
  // SPAWN
  // --------------------------------------------------------

  // Server-driven spawn (ML-FG backend). The fish is placed on its path before its first frame.
  internal void InitializeFromServer(FishData visual, ServerPath path, float hitboxRadius, List<HitSegment> serverSegments)
  {
    Path = path;
    SetSegments(hitboxRadius, serverSegments);
    BeginLife(visual);
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
    PrepareColliders();
    ResetLegacyState();
    SetupVisuals();
  }

  private void SetupVisuals()
  {
    imageAnimation.StopAnimation();
    imageAnimation.SetAnimationData(data.animationFrames, data.animationSpeed, data.loop);
    imageAnimation.StartAnimation();
    baseAnimationSpeed = data.animationSpeed;

    // The pivot moves the art relative to the path point; the hit circles stay on it.
    rect.pivot = data.pivot;
    rect.sizeDelta = data.spriteSize;

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

    transform.position = Playfield.ServerToWorld(pos);

    // Near/past the end a clamped look-ahead lands behind the (extrapolated) position and
    // reverses the heading, so sample backwards there instead.
    Vector2 dir = progress >= 1.0 - HeadingLookAhead
      ? pos - Path.PositionAt(progress - HeadingLookAhead)
      : Path.PositionAt(progress + HeadingLookAhead) - pos;
    if (dir.sqrMagnitude > 0.0001f)
      ApplyHeading(Playfield.ServerDirToLocal(dir.normalized));

    // After the pose is applied, so the collider offsets account for rotation and flip.
    PlaceColliders();

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

  // The fish's hit circles at nowMs in server px. segments[0] (offset 0) is the head at the
  // path position; each other circle sits `offset` px behind it along the path (extrapolated
  // before the path start, so the tail trails the head even off-screen).
  internal int GetHitCircles(double nowMs, List<HitCircle> into)
  {
    into.Clear();
    if (Path == null || segments == null)
      return 0;

    if (segments.Length == 1 && segments[0].offset == 0f)
    {
      into.Add(new HitCircle { center = Path.PositionAtTime(nowMs), radius = segments[0].radius });
      return 1;
    }

    double head = Path.DistanceAt(nowMs);
    for (int i = 0; i < segments.Length; i++)
      into.Add(new HitCircle { center = Path.PositionAtDistance(head - segments[i].offset), radius = segments[i].radius });
    return into.Count;
  }

  // Point-in-circle against every hit circle, like the server.
  internal bool ContainsServerPoint(Vector2 point, double nowMs)
  {
    int count = GetHitCircles(nowMs, circleScratch);
    for (int i = 0; i < count; i++)
    {
      HitCircle c = circleScratch[i];
      if ((point - c.center).sqrMagnitude <= c.radius * c.radius)
        return true;
    }
    return false;
  }

  // --------------------------------------------------------
  // HITBOX COLLIDERS (pointer picking / target lock)
  // --------------------------------------------------------

  private void SetSegments(float hitboxRadius, List<HitSegment> serverSegments)
  {
    float headRadius = hitboxRadius > 0f ? hitboxRadius : DefaultHitboxRadius;

    if (serverSegments != null && serverSegments.Count > 0)
    {
      segments = new HitSegment[serverSegments.Count];
      for (int i = 0; i < segments.Length; i++)
      {
        HitSegment src = serverSegments[i];
        segments[i] = new HitSegment
        {
          offset = src?.offset ?? 0f,
          radius = src != null && src.radius > 0f ? src.radius : headRadius
        };
      }
      HitboxRadius = segments[0].radius;
    }
    else
    {
      segments = new[] { new HitSegment { offset = 0f, radius = headRadius } };
      HitboxRadius = headRadius;
    }
  }

  private void PrepareColliders()
  {
    hitColliders.RemoveAll(c => c == null);

    // Only fish with more circles than the prefab holds (the dragon) add any, once per instance.
    int needed = segments?.Length ?? 0;
    while (hitColliders.Count < needed)
    {
      var col = gameObject.AddComponent<CircleCollider2D>();
      col.isTrigger = true;
      hitColliders.Add(col);
    }

    activeColliderCount = needed;
    for (int i = 0; i < hitColliders.Count; i++)
    {
      hitColliders[i].enabled = i < needed;
      hitColliders[i].offset = Vector2.zero;
    }
  }

  // Server px -> this fish's collider units. Goes through the playfield and the fish's own
  // scale, so canvas scaling, portrait rotation and the facing flip can't distort it.
  private float PxToColliderUnits()
  {
    float worldPerPx = (Playfield.ServerToWorld(Vector2.right) - Playfield.ServerToWorld(Vector2.zero)).magnitude;
    Vector3 s = transform.lossyScale;
    float scale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y)); // CircleCollider2D scales by the larger axis
    return scale > 0f ? worldPerPx / scale : worldPerPx;
  }

  private void PlaceColliders()
  {
    if (activeColliderCount == 0)
      return;

    float pxToUnits = PxToColliderUnits();
    int count = GetHitCircles(ServerClock.NowMs, circleScratch);
    for (int i = 0; i < count && i < activeColliderCount; i++)
    {
      CircleCollider2D col = hitColliders[i];
      float radius = circleScratch[i].radius * pxToUnits;
      if (!Mathf.Approximately(col.radius, radius))
        col.radius = radius;

      // The head sits on the pivot; only trailing segments need an offset.
      if (segments[i].offset != 0f)
        col.offset = transform.InverseTransformPoint(Playfield.ServerToWorld(circleScratch[i].center));
    }
  }

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
    segments = null;
    for (int i = 0; i < hitColliders.Count; i++)
    {
      hitColliders[i].enabled = false;
      hitColliders[i].offset = Vector2.zero;
    }
    activeColliderCount = 0;
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
    for (int i = 0; i < activeColliderCount; i++)
      hitColliders[i].enabled = pickable;
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

  // Targetable (torpedo/laser lock) when a hit circle's center is inside the 1920x1080
  // playfield, tested in server px like the server and bullets, independent of the camera.
  // viewportPadding is a fraction of the playfield. Skips the first two frames so a freshly
  // pooled fish isn't reported at its pre-spawn position.
  private void UpdateViewportVisibility()
  {
    IsVisibleInViewport = false;

    if (State != FishState.Alive || PendingVisualDeath || !IsServerDriven || Time.frameCount - aliveSinceFrame < 2)
      return;

    IsVisibleInViewport = TryGetAimServerPoint(ServerClock.NowMs, out _);
  }

  // Torpedo/laser aim point in server px: the center of the first hit circle (config order)
  // that is inside the playfield. False when none is; `point` is then the first circle's center.
  // Frozen or dying fish use the circles as last placed, since their path keeps advancing.
  internal bool TryGetAimServerPoint(double nowMs, out Vector2 point)
  {
    int count;
    if (IsServerDriven && !movementFrozen)
    {
      count = GetHitCircles(nowMs, circleScratch);
    }
    else
    {
      circleScratch.Clear();
      for (int i = 0; i < activeColliderCount; i++)
        circleScratch.Add(new HitCircle { center = Playfield.WorldToServer(transform.TransformPoint(hitColliders[i].offset)) });
      count = circleScratch.Count;
    }

    if (count == 0)
    {
      point = Playfield.WorldToServer(transform.position);
      return false;
    }

    float padX = viewportPadding * Playfield.Width;
    float padY = viewportPadding * Playfield.Height;
    for (int i = 0; i < count; i++)
    {
      Vector2 c = circleScratch[i].center;
      if (c.x >= padX && c.x <= Playfield.Width - padX && c.y >= padY && c.y <= Playfield.Height - padY)
      {
        point = c;
        return true;
      }
    }

    point = circleScratch[0].center;
    return false;
  }

  internal Vector2 AimServerPointAt(double nowMs)
  {
    TryGetAimServerPoint(nowMs, out Vector2 point);
    return point;
  }

  // World-space aim point for torpedo/laser visuals (same point the SHOOT reports).
  internal Vector3 GetAimPoint() => Playfield.ServerToWorld(AimServerPointAt(ServerClock.NowMs));
}
