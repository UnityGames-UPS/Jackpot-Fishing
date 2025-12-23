using UnityEngine;
using DG.Tweening;
using System;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
internal class Fish : MonoBehaviour
{
  internal int FishId;
  protected Tween movementTween;
  protected ImageAnimation imageAnimation;
  protected Image fishImage;
  protected Tween damageTween;
  internal Transform HitPoint => transform;

  internal RectTransform Rect => GetComponent<RectTransform>();

  [Header("Offset Settings")]
  [SerializeField] protected float offScreenOffset = 5f; // Distance to start off-screen

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

    // Add offset starting point for specific fish (like dragon)
    if (ShouldUseOffset(data.fishId))
    {
      path = AddOffset(path);
    }

    transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

    transform.position = path[0];
    MoveAlongPath(path, data.minInterval / 1000f);
  }

  protected virtual bool ShouldUseOffset(string fishId)
  {
    // Add fish IDs that need offset starting points
    return fishId == "dragon";
  }

  protected Vector3[] AddOffset(Vector3[] originalPath)
  {
    if (originalPath.Length < 2) return originalPath;

    // Calculate direction from first to second point for start offset
    Vector3 startDirection = (originalPath[1] - originalPath[0]).normalized;
    Vector3 startOffsetPoint = originalPath[0] - startDirection * offScreenOffset;

    // Calculate direction from second-to-last to last point for end offset
    Vector3 endDirection = (originalPath[originalPath.Length - 1] - originalPath[originalPath.Length - 2]).normalized;
    Vector3 endOffsetPoint = originalPath[originalPath.Length - 1] + endDirection * offScreenOffset;

    // Create new path with offset points at start and end
    Vector3[] newPath = new Vector3[originalPath.Length + 2];
    newPath[0] = startOffsetPoint;
    for (int i = 0; i < originalPath.Length; i++)
    {
      newPath[i + 1] = originalPath[i];
    }
    newPath[newPath.Length - 1] = endOffsetPoint;

    return newPath;
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

  internal void PlayLaserImpact()
  {
  }

  internal void StopLaserImpact()
  {
  }
}
