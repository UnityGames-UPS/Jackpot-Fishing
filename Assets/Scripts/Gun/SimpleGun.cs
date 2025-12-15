using UnityEngine;
using DG.Tweening;

public class SimpleGun : BaseGun
{
  [SerializeField] private BulletPool bulletPool;

  [Header("Recoil")]
  [SerializeField] private float recoilDistance = 0.15f;
  [SerializeField] private float recoilDuration = 0.08f;
  [SerializeField] private float returnDuration = 0.12f;
  [SerializeField] private Ease recoilEase = Ease.OutQuad;

  private Tween recoilTween;
  private Vector3 initialLocalPos;

  void Awake()
  {
    initialLocalPos = transform.localPosition;
  }

  internal override void Fire()
  {
    PlayRecoil();

    BulletController bullet = bulletPool.GetFromPool();
    bullet.InitBullet(bulletPool);
    bullet.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
    bullet.Fire(muzzle.up);
  }

  void PlayRecoil()
  {
    recoilTween?.Kill();

    Vector3 recoilDir = -transform.up; // opposite of firing direction

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

}
