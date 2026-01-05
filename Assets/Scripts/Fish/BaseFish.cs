using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using FluffyUnderware.Curvy;
using FluffyUnderware.Curvy.Controllers;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(SplineController))]
internal class BaseFish : MonoBehaviour
{
  [SerializeField] internal FishData data;
  internal int FishId;
  protected Tween movementTween;
  protected ImageAnimation imageAnimation;
  protected Image fishImage;
  protected Tween damageTween;
  internal Transform HitPoint => transform.GetChild(0);
  internal RectTransform Rect => GetComponent<RectTransform>();

  private SplineController splineController;

  internal virtual void DamageAnimation() { }

  internal virtual void Initialize(FishData data)
  {
    this.data = data;
    imageAnimation = GetComponent<ImageAnimation>();
    fishImage = GetComponent<Image>();
    BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();

    if (!string.IsNullOrEmpty(data.fishId))
    {
      if (this.data != null && imageAnimation != null)
      {
        imageAnimation.SetAnimationData(
          this.data.animationFrames,
          this.data.animationSpeed,
          this.data.loop
        );
        imageAnimation.StartAnimation();

        Rect.sizeDelta = this.data.spriteSize;

        if (boxCollider != null)
        {
          boxCollider.size = this.data.colliderSize;
          boxCollider.offset = this.data.colliderOffset;
        }
        HitPoint.localPosition = new Vector3(this.data.colliderOffset.x, this.data.colliderOffset.y, 0);
      }
    }

    fishImage.color = new Color(Color.white.r, Color.white.g, Color.white.b, a: 0);
    fishImage.DOFade(1, 0.2f);

    SetupCurvyMovement();
  }

  private void SetupCurvyMovement()
  {
    splineController = GetComponent<SplineController>();

    bool moveRightToLeft = Random.value > 0.5f;

    CurvySpline spline = CurvyPathProvider.Instance.GetRandomSpline(moveRightToLeft);

    if (spline == null)
    {
      Debug.LogError("Null Spline");
      FishManager.Instance.DespawnFish(this);
      return;
    }

    splineController.Spline = spline;
    splineController.MoveMode = CurvyController.MoveModeEnum.AbsolutePrecise;
    splineController.OrientationMode = OrientationModeEnum.Tangent;
    splineController.OrientationAxis = moveRightToLeft ? OrientationAxisEnum.Left : OrientationAxisEnum.Right;
    splineController.Clamping = CurvyClamping.Clamp;

    float visibleTimeSec = data.duration / 1000f;
    float speed = spline.Length / visibleTimeSec;

    splineController.Speed = speed;
    splineController.Position = 0;

    FlipSprite(faceRight: !moveRightToLeft);

    splineController.PlayAutomatically = true;

    splineController.OnEndReached.RemoveListener(OnPathComplete);
    splineController.OnEndReached.AddListener(OnPathComplete);
    // Debug.Log($"Fish using spline {spline.name} | Dir: {(moveRightToLeft ? "RL" : "LR")}");
  }

  private void FlipSprite(bool faceRight)
  {
    Vector3 scale = transform.localScale;
    scale.x = Mathf.Abs(scale.x) * (faceRight ? -1 : 1);
    transform.localScale = scale;
  }

  private void OnPathComplete(CurvySplineMoveEventArgs args)
  {
    fishImage.DOFade(0, 0.2f).OnComplete(() =>
    {
      FishManager.Instance.DespawnFish(this);
    });
  }

  internal virtual void Die()
  {
    fishImage.DOFade(0, 0.2f).OnComplete(() =>
    {
      FishManager.Instance.DespawnFish(this);
    });
  }
  internal virtual void ResetFish()
  {
    movementTween?.Kill();
    damageTween?.Kill();

    if (fishImage != null)
      fishImage.color = Color.white;

    transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    transform.localScale = Vector3.one;
  }
  internal void PlayLaserImpact() { }

  internal void StopLaserImpact() { }
}
