using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using FluffyUnderware.Curvy;
using FluffyUnderware.Curvy.Controllers;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(SplineController))]
internal class BaseFish : MonoBehaviour
{
  internal int FishId;
  protected Tween movementTween;
  protected ImageAnimation imageAnimation;
  protected Image fishImage;
  protected Tween damageTween;

  internal Transform HitPoint => transform;
  internal RectTransform Rect => GetComponent<RectTransform>();

  private SplineController splineController;

  internal virtual void DamageAnimation() { }

  internal virtual void Initialize(FishData data)
  {
    imageAnimation = GetComponent<ImageAnimation>();
    fishImage = GetComponent<Image>();
    BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();

    if (!string.IsNullOrEmpty(data.fishId))
    {
      var visualData = FishManager.Instance.GetVisualData(data.fishId);
      if (visualData != null && imageAnimation != null)
      {
        imageAnimation.SetAnimationData(
          visualData.animationFrames,
          visualData.animationSpeed,
          visualData.loop
        );
        imageAnimation.StartAnimation();

        Rect.sizeDelta = visualData.spriteSize;

        if (boxCollider != null)
        {
          boxCollider.size = visualData.colliderSize;
          boxCollider.offset = visualData.colliderOffset;
        }
      }
    }

    SetupCurvyMovement(data);
  }

  private void SetupCurvyMovement(FishData data)
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

    float visibleTimeSec = data.minInterval / 1000f;
    float speed = spline.Length / visibleTimeSec;

    splineController.Speed = speed;
    splineController.Position = 0;

    // Sprite faces LEFT by default
    // RL spline → face left (no flip)
    // LR spline → face right (flip)
    FlipSprite(faceRight: !moveRightToLeft);

    splineController.PlayAutomatically = true;

    splineController.OnEndReached.RemoveListener(OnPathComplete);
    splineController.OnEndReached.AddListener(OnPathComplete);
    Debug.Log($"Fish using spline {spline.name} | Dir: {(moveRightToLeft ? "RL" : "LR")}");
  }

  private void FlipSprite(bool faceRight)
  {
    Vector3 scale = transform.localScale;
    scale.x = Mathf.Abs(scale.x) * (faceRight ? -1 : 1);
    transform.localScale = scale;
  }

  private void OnPathComplete(CurvySplineMoveEventArgs args)
  {
    FishManager.Instance.DespawnFish(this);
  }

  internal virtual void Die()
  {
    FishManager.Instance.DespawnFish(this);
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
