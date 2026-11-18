using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Normal bullet, simulated in server space exactly like the backend's reference client:
// constant speed, bounces off the 1920x1080 edges (each axis hit counts as one bounce),
// point-in-circle hit test against each fish's server hit circles (hitboxRadius, or segments).
//
// Every bullet resolves exactly once and sends exactly one SHOOT, which settles the shot
// FIRE charged for: on a hit, when it bounces more than maxBulletBounces, or when its lifetime ends.
// Remote (opponent) bullets run the same simulation for show and never send anything.
public class BulletView : MonoBehaviour
{
  private const float MaxStep = 1f / 60f; // substep long frames so bullets can't tunnel through fish
  private const float FastForwardStep = 0.01f; // latency compensation steps (s)
  private const float MaxFastForward = 0.5f;   // never fast-forward more than this (s)

  [SerializeField] private float serverSpeed = 1200f; // server px per second (matches the reference client)
  [SerializeField] private float maxLifetime = 10f; // safety net; bounces normally end a miss first

  [Header("Bet Tier Art")]
  [SerializeField] private Image bulletImage;
  [SerializeField] private TierSprites bulletSprites;

  [Header("On Fish Hit")]
  [SerializeField] private Color fishDamageColor;

  internal static int InFlight { get; private set; } // own bullets only
  internal static int RemoteInFlight { get; private set; }

  // Opponent bullets in flight, so the server's shot_result can end them by bulletId.
  private static readonly Dictionary<string, BulletView> remoteById = new();

  private string bulletId;
  private Vector2 origin;   // the seat, as reported to the server
  private Vector2 lastBouncePoint; // where the server raycasts from; the seat until the first bounce
  private int betIndex;     // the bet this bullet was fired with
  private Vector2 position; // server px
  private Vector2 velocity; // server px/s
  private int bounceCount;
  private int maxBounces;
  private float age;
  private bool active;
  private bool remote;
  private GunTier tier; // bet tier this bullet was fired at; picks the bullet and hit-marker art

  void OnValidate()
  {
    if (bulletImage == null)
      bulletImage = GetComponent<Image>();
  }

  void Awake()
  {
    if (bulletImage == null)
      bulletImage = GetComponent<Image>();
    // Hits are tested in server space, not by physics.
    if (TryGetComponent<Collider2D>(out var col))
      col.enabled = false;
    if (TryGetComponent<Rigidbody2D>(out var rb))
      rb.bodyType = RigidbodyType2D.Kinematic;
  }

  internal void Fire(string id, Vector2 serverOrigin, Vector2 serverStart, Vector2 serverDirection, int maxBulletBounces, float? speed, int firedBetIndex)
  {
    remote = false;
    origin = serverOrigin;
    lastBouncePoint = serverOrigin;
    betIndex = firedBetIndex;
    SetTier(GunTiers.ForBet(firedBetIndex));
    Launch(id, serverStart, serverDirection, maxBulletBounces, speed);
    InFlight++;
  }

  // elapsedSec: time since the server's firedAt. The bullet starts where it is by now on the
  // shooter's screen, bouncing off the walls on the way (no hit tests: shot_result ends it).
  internal void FireRemote(string id, Vector2 serverStart, Vector2 serverDirection, int maxBulletBounces, float? speed, float elapsedSec, GunTier firedTier)
  {
    remote = true;
    SetTier(firedTier);
    origin = serverStart;
    Launch(id, serverStart, serverDirection, maxBulletBounces, speed);
    RemoteInFlight++;
    if (!string.IsNullOrEmpty(id))
      remoteById[id] = this;

    float fastForward = Mathf.Clamp(elapsedSec, 0f, MaxFastForward);
    float remaining = fastForward;
    while (remaining > 0f)
    {
      float step = Mathf.Min(remaining, FastForwardStep);
      remaining -= step;
      position += velocity * step;
      Bounce();
      if (bounceCount > maxBounces)
      {
        Finish();
        return;
      }
    }
    age = fastForward;
    Render();
  }

  // shot_result for an opponent's bullet: it ends now, with a hit marker at aimTarget on a hit.
  // MISS_GATE just removes it. Unknown ids (already ended locally) are ignored.
  internal static void ResolveRemote(string id, Vector2? serverAimTarget, bool hit)
  {
    if (string.IsNullOrEmpty(id) || !remoteById.TryGetValue(id, out var bullet))
      return;

    if (hit && serverAimTarget.HasValue)
      HitMarkerPool.Instance.GetFromPool().Play(Playfield.ServerToWorld(serverAimTarget.Value), bullet.tier);
    bullet.Finish();
  }

  // Pooled bullets are shared by every rig, so the art is set on each fire.
  private void SetTier(GunTier firedTier)
  {
    tier = firedTier;
    bulletSprites.ApplyTo(bulletImage, firedTier);
  }

  // speed: the weapon's server px/s, or null for the serialized default.
  private void Launch(string id, Vector2 serverStart, Vector2 serverDirection, int maxBulletBounces, float? speed)
  {
    bulletId = id;
    position = serverStart;
    velocity = serverDirection.normalized * (speed ?? serverSpeed);
    bounceCount = 0;
    maxBounces = maxBulletBounces;
    age = 0f;
    active = true;

    Render();
  }

  void Update()
  {
    if (!active)
      return;

    float remaining = Time.deltaTime;
    while (active && remaining > 0f)
    {
      float step = Mathf.Min(remaining, MaxStep);
      remaining -= step;
      Step(step);
    }

    if (active)
      Render();
  }

  private void Step(float dt)
  {
    position += velocity * dt;
    Bounce();

    if (bounceCount > maxBounces)
    {
      ResolveMiss();
      return;
    }

    double now = ServerClock.NowMs;
    BaseFish hit = FindHitFish(now);
    if (hit != null)
    {
      ResolveHit(hit, now);
      return;
    }

    age += dt;
    if (age >= maxLifetime)
      ResolveMiss();
  }

  private void Bounce()
  {
    int bouncesBefore = bounceCount;

    if (position.x <= 0f)
    {
      position.x = 0f;
      velocity.x = -velocity.x;
      bounceCount++;
    }
    else if (position.x >= Playfield.Width)
    {
      position.x = Playfield.Width;
      velocity.x = -velocity.x;
      bounceCount++;
    }

    if (position.y <= 0f)
    {
      position.y = 0f;
      velocity.y = -velocity.y;
      bounceCount++;
    }
    else if (position.y >= Playfield.Height)
    {
      position.y = Playfield.Height;
      velocity.y = -velocity.y;
      bounceCount++;
    }

    if (bounceCount != bouncesBefore)
      lastBouncePoint = position;
  }

  // First fish (in spawn order) whose server hitbox contains the bullet, like the server.
  private BaseFish FindHitFish(double now)
  {
    var fishes = FishManager.Instance.GetActiveFishes();
    for (int i = 0; i < fishes.Count; i++)
    {
      BaseFish fish = fishes[i];
      if (!fish.IsHittable(now))
        continue;

      if (fish.ContainsServerPoint(position, now)) // every hit circle, incl. dragon segments
        return fish;
    }
    return null;
  }

  private void ResolveHit(BaseFish fish, double now)
  {
    if (!remote)
    {
      if (GameLog.IsOn(LogCategory.Bullet))
        GameLog.Info(LogCategory.Bullet, $"Hit {bulletId} -> {fish.Id} ({fish.data?.variant}) at ({position.x:F0},{position.y:F0}) bounces={bounceCount}");
      SocketIOManager.Instance.SendShoot(SocketIOManager.Instance.NormalWeaponId, bulletId, origin, lastBouncePoint, position, bounceCount, betIndex, fish.Id, now);
    }

    Vector3 world = Playfield.ServerToWorld(position);
    HitMarkerPool.Instance.GetFromPool().Play(world, tier);
    fish.StartCoroutine(fish.DamageAnimation(fishDamageColor));
    Finish();
  }

  private void ResolveMiss()
  {
    if (remote)
    {
      Finish();
      return;
    }

    if (GameLog.IsOn(LogCategory.Bullet))
      GameLog.Info(LogCategory.Bullet, $"Miss {bulletId} at ({position.x:F0},{position.y:F0}) bounces={bounceCount} age={age:F1}s");
    SocketIOManager.Instance.SendShoot(SocketIOManager.Instance.NormalWeaponId, bulletId, origin, lastBouncePoint, position, bounceCount, betIndex, null, ServerClock.NowMs);
    Finish();
  }

  // FIRE was refused. The pooled bullet may already be flying as a different shot, so match the id.
  internal void CancelShot(string id)
  {
    if (active && !remote && bulletId == id)
      Finish();
  }

  private void Finish()
  {
    if (!active)
      return;

    active = false;
    ReleaseCount();
    BulletPool.Instance.ReturnToPool(this);
  }

  private void ReleaseCount()
  {
    if (remote && bulletId != null && remoteById.TryGetValue(bulletId, out var tracked) && tracked == this)
      remoteById.Remove(bulletId);

    if (remote)
      RemoteInFlight = Mathf.Max(0, RemoteInFlight - 1);
    else
      InFlight = Mathf.Max(0, InFlight - 1);
  }

  private void Render()
  {
    Vector3 world = Playfield.ServerToWorld(position);
    Vector3 ahead = Playfield.ServerToWorld(position + velocity.normalized);
    transform.position = world;
    transform.up = ahead - world;
  }

  void OnDisable()
  {
    if (!active)
      return;

    // Should never happen.
    if (!remote)
      GameLog.Warn(LogCategory.Bullet, $"{bulletId} disabled before resolving; no SHOOT was sent");
    active = false;
    ReleaseCount();
  }

  // Old-backend physics hit (legacy-spawned fish only). Server-driven fish never reach this.
  internal void OnFishHit(BaseFish fish)
  {
    if (!active)
      return;

    HitMarkerPool.Instance.GetFromPool().Play(transform.position, tier);
    StartCoroutine(fish.DamageAnimation(fishDamageColor));
    Finish();
  }
}
