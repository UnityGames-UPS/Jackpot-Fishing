using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
public class SimpleGun : BaseGun
{
  [Header("Recoil")]
  [SerializeField] private float recoilDistance = 0.15f;
  [SerializeField] private float recoilDuration = 0.08f;
  [SerializeField] private float returnDuration = 0.12f;
  [SerializeField] private Ease recoilEase = Ease.OutQuad;

  [Header("Muzzle Flash")]
  [SerializeField] private float muzzleFadeIn = 0.03f;
  [SerializeField] private float muzzleFadeOut = 0.08f;

  [Header("Firing")]
  [Tooltip("Shots per second, used only until init sends the weapon's fireIntervalMs.")]
  [SerializeField] protected float fireRate = 6f;
  [SerializeField] private int maxBulletsInFlight = 30;

  [Header("Bet Tier Art")]
  [SerializeField] private Image gunImage;
  [SerializeField] private TierSprites gunSprites;

  internal GunTier Tier { get; private set; }

  internal float FireInterval =>
    SocketIOManager.Instance != null
      ? SocketIOManager.Instance.FireIntervalFor(SocketIOManager.Instance.NormalWeaponId, 1f / fireRate)
      : 1f / fireRate;
  private Image muzzleImage;
  private Tween recoilTween;
  private Tween muzzleTween;
  private Vector3 initialLocalPos;
  private float nextAllowedFireTime;

  void OnValidate()
  {
    if (gunImage == null)
      gunImage = GetComponent<Image>();
  }

  internal void Awake()
  {
    if (gunImage == null)
      gunImage = GetComponent<Image>();
    initialLocalPos = transform.localPosition;
    muzzleImage = muzzle.GetComponent<Image>();

    if (muzzleImage != null)
      muzzleImage.color = new Color(1, 1, 1, 0); // start hidden
  }

  // Gun art for the bet's tier (Base / Buff / Buffed). Bullets and hit markers pick theirs per shot.
  internal void SetTier(GunTier tier)
  {
    Tier = tier;
    gunSprites.ApplyTo(gunImage, tier);
  }

  internal override void Fire()
  {
    TryFire();
  }

  internal void TryFire()
  {
    if (Time.time < nextAllowedFireTime)
      return;

    var socket = SocketIOManager.Instance;
    if (socket == null || !socket.CanShoot || !Playfield.IsReady)
      return;

    if (BulletView.InFlight >= maxBulletsInFlight)
      return;

    if (!UIManager.Instance.CanAffordShot())
      return;

    string bulletId = "bullet_" + Guid.NewGuid().ToString("N").Substring(0, 12);
    int betIndex = UIManager.Instance.BetCounter;

    Vector2 origin = socket.SeatOrigin;
    Vector2 start = Playfield.WorldToServer(muzzle.position);
    Vector2 direction = Playfield.WorldDirToServer(muzzle.up).normalized;
    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

    if (GameLog.IsOn(LogCategory.Bullet))
      GameLog.Info(LogCategory.Bullet, $"Fire {bulletId} from ({start.x:F0},{start.y:F0}) angle {angle:F1}, {BulletView.InFlight + 1} in flight");

    PlayMuzzleFlash();
    PlayRecoil();
    BulletView bullet = BulletPool.Instance.GetFromPool();
    bullet.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
    string weapon = socket.NormalWeaponId;
    bullet.Fire(bulletId, origin, start, direction, socket.BouncesFor(weapon), socket.BulletSpeedFor(weapon), betIndex);
    socket.SendFire(weapon, bulletId, origin, angle, betIndex, null, () => bullet.CancelShot(bulletId));
    nextAllowedFireTime = Time.time + FireInterval;
  }

  // Opponent's bullet_fired: visual only. No balance reservation, FIRE or SHOOT.
  // firedAt (server ms) fast-forwards the bullet to where it is now on the shooter's screen.
  internal void PlayRemoteShot(string bulletId, string weaponType, float serverAngle, float? bulletSpeed, double? firedAt, int? betIndex)
  {
    var socket = SocketIOManager.Instance;
    if (socket == null || !Playfield.IsReady)
      return;

    float rad = serverAngle * Mathf.Deg2Rad;
    Vector2 direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    RotateGun(transform.position + Playfield.ServerDirToWorld(direction));

    PlayMuzzleFlash();
    PlayRecoil();
    BulletView bullet = BulletPool.Instance.GetFromPool();
    bullet.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
    // Simulated with the shooter's weapon settings, so it bounces like it does on their screen.
    string weapon = string.IsNullOrEmpty(weaponType) ? socket.NormalWeaponId : weaponType;
    float? speed = bulletSpeed > 0f ? bulletSpeed : socket.BulletSpeedFor(weapon);
    float elapsed = firedAt.HasValue && ServerClock.IsSynced
      ? Mathf.Max(0f, (float)((ServerClock.NowMs - firedAt.Value) / 1000.0))
      : 0f;
    GunTier tier = betIndex.HasValue ? GunTiers.ForBet(betIndex.Value) : Tier;
    bullet.FireRemote(bulletId, Playfield.WorldToServer(muzzle.position), direction, socket.BouncesFor(weapon), speed, elapsed, tier);
  }

  void PlayRecoil()
  {
    recoilTween?.Kill();

    Vector3 recoilDir = transform.localRotation * Vector3.down; // opposite of firing direction, in parent space

    recoilTween = transform
        .DOLocalMove(initialLocalPos + recoilDir * recoilDistance, recoilDuration)
        .SetEase(recoilEase)
        .OnComplete(() =>
        {
          transform
              .DOLocalMove(initialLocalPos, returnDuration)
              .SetEase(Ease.OutQuad);
        });
  }

  void PlayMuzzleFlash()
  {
    if (muzzleImage == null) return;

    muzzleTween?.Kill();

    muzzleTween = DOTween.Sequence()
        .Append(muzzleImage.DOFade(0.7f, muzzleFadeIn))
        .Append(muzzleImage.DOFade(0f, muzzleFadeOut));
  }
}
