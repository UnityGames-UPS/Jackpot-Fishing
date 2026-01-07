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
  [SerializeField] protected float fireRate = 6f;

  internal override float FireInterval => 1f / fireRate;
  private Image muzzleImage;
  private Tween recoilTween;
  private Tween muzzleTween;
  private Vector3 initialLocalPos;

  internal override void Awake()
  {
    base.Awake();
    initialLocalPos = transform.localPosition;
    muzzleImage = muzzle.GetComponent<Image>();

    if (muzzleImage != null)
      muzzleImage.color = new Color(1, 1, 1, 0); // start hidden
  }

  internal override void Fire()
  {
    PlayMuzzleFlash();
    PlayRecoil();
    BulletView bullet = BulletPool.Instance.GetFromPool();
    bullet.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
    bullet.Fire(muzzle.up);
    DeductBalance();
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

  void PlayMuzzleFlash()
  {
    if (muzzleImage == null) return;

    muzzleTween?.Kill();

    muzzleTween = DOTween.Sequence()
        .Append(muzzleImage.DOFade(0.7f, muzzleFadeIn))
        .Append(muzzleImage.DOFade(0f, muzzleFadeOut));
  }


  void DeductBalance()
  {
    float gunCost = UIManager.Instance?.currentBet * SocketIOManager.Instance?.GunCosts[0] ?? 0;
    float newBalance = UIManager.Instance?.currentBalance - gunCost ?? 0;
    // Debug.Log(UIManager.Instance?.currentBet + " " + SocketIOManager.Instance?.GunCosts[0]);
    // Debug.Log(newBalance);
    UIManager.Instance.UpdateBalance(newBalance);
  }
}
