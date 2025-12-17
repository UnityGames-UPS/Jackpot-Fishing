using UnityEngine;
using DG.Tweening;
using System;
using UnityEngine.UI;

internal class Fish : MonoBehaviour
{
  internal int FishId;
  protected Tween movementTween;
  protected ImageAnimation imageAnimation;
  protected Image fishImage;
  protected Tween damageTween;
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

        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform != null)
        {
          rectTransform.sizeDelta = visualData.spriteSize;
        }

        if (boxCollider != null)
        {
          boxCollider.size = visualData.colliderSize;
          boxCollider.offset = visualData.colliderOffset;
        }
      }
      else
      {
        Debug.LogError($"Visual data not found for :{data.fishId}");
      }
    }
    else
    {
      Debug.LogError("fishId not found!");
    }

    Transform[] fishPath = PathManager.Instance.GetRandomPath();
    if (fishPath == null || fishPath.Length == 0)
    {
      Debug.LogError($"Invalid path");
      FishManager.Instance.DespawnFish(this);
      return;
    }

    Vector3[] path = new Vector3[fishPath.Length];
    for (int i = 0; i < fishPath.Length; i++)
    {
      path[i] = fishPath[i].position;
    }

    bool moveRightToLeft = UnityEngine.Random.value > 0.5f;
    if (moveRightToLeft)
    {
      Array.Reverse(path);
    }

    transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

    transform.position = path[0];
    MoveAlongPath(path, data.minInterval / 1000f);
  }

  internal virtual void MoveAlongPath(Vector3[] path, float duration)
  {
    movementTween?.Kill();
    movementTween = transform.DOPath(
          path,
          duration,
          PathType.CatmullRom,
          PathMode.Sidescroller2D,
          10,
          Color.black
      )
      .SetEase(Ease.Linear)
      .SetLookAt(0.01f) // Makes fish rotate smoothly to follow path curves
      .OnComplete(() => FishManager.Instance.DespawnFish(this));
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
}
