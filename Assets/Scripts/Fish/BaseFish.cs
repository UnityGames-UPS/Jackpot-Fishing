using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using FluffyUnderware.Curvy;
using FluffyUnderware.Curvy.Controllers;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(SplineController))]
[RequireComponent(typeof(Image))]
[RequireComponent(typeof(ImageAnimation))]
[RequireComponent(typeof(BoxCollider2D))]
internal class BaseFish : MonoBehaviour
{
  [SerializeField] internal FishData data;
  internal Transform HitPoint => transform.GetChild(0);
  internal RectTransform Rect => GetComponent<RectTransform>();
  protected Tween movementTween;
  protected ImageAnimation imageAnimation;
  protected Image fishImage;
  protected Tween damageTween;
  protected float baseSpeed;
  protected float speedMultiplier = 1f;
  protected SplineController splineController;
  protected BoxCollider2D boxCollider;
  internal virtual void DamageAnimation() { }
  internal virtual void Die() => DespawnFish();

  internal virtual void Initialize(FishData data)
  {
    splineController = GetComponent<SplineController>();
    imageAnimation = GetComponent<ImageAnimation>();
    fishImage = GetComponent<Image>();
    boxCollider = GetComponent<BoxCollider2D>();

    boxCollider.enabled = true;
    this.data = data;

    if (!string.IsNullOrEmpty(data.variant))
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
    bool moveRightToLeft = Random.value > 0.5f;
    FlipSprite(faceRight: !moveRightToLeft);

    CurvySpline spline = CurvyPathProvider.Instance.GetRandomSpline(moveRightToLeft);

    if (spline == null)
    {
      Debug.LogError("Null Spline");
      DespawnFish();
      return;
    }

    splineController.Spline = spline;
    splineController.Position = 0;
    splineController.MoveMode = CurvyController.MoveModeEnum.AbsolutePrecise;
    splineController.OrientationMode = OrientationModeEnum.Tangent;
    splineController.OrientationAxis = moveRightToLeft ? OrientationAxisEnum.Left : OrientationAxisEnum.Right;
    splineController.Clamping = CurvyClamping.Clamp;
    splineController.PlayAutomatically = true;

    float visibleTimeSec = data.duration / 1000f;
    baseSpeed = spline.Length / visibleTimeSec;
    ApplySpeed();

    splineController.OnEndReached.RemoveListener(OnPathComplete);
    splineController.OnEndReached.AddListener(OnPathComplete);

    splineController.Play();
    // Debug.Log($"Fish using spline {spline.name} | Dir: {(moveRightToLeft ? "RL" : "LR")}");
  }

  protected void ApplySpeed()
  {
    splineController.Speed = baseSpeed * speedMultiplier;
  }

  private void FlipSprite(bool faceRight)
  {
    Vector3 scale = transform.localScale;
    scale.x = Mathf.Abs(scale.x) * (faceRight ? -1 : 1);
    transform.localScale = scale;
  }

  internal void SetSpeedMultiplier(float multiplier)
  {
    speedMultiplier = multiplier;
    ApplySpeed();
  }

  private void OnPathComplete(CurvySplineMoveEventArgs args)
  {
    DespawnFish();
  }

  protected void DespawnFish()
  {
    fishImage.DOFade(0, 0.2f).OnComplete(() =>
    {
      FishManager.Instance.DespawnFish(this);
      ResetFish();
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

    if (splineController != null)
    {
      splineController.Stop(); 
      splineController.PlayAutomatically = false;
    }

    speedMultiplier = 1f;
    ApplySpeed();
  }

  internal void PlayLaserImpact() { }

  internal void StopLaserImpact() { }
}
