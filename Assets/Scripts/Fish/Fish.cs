using UnityEngine;
using DG.Tweening;

internal class Fish : MonoBehaviour
{
  internal int FishId;
  protected Tween movementTween;

  [Header("Sprite Settings")]
  [SerializeField] private bool flipSpriteForLeftMovement = true;

  internal virtual void Initialize(FishData data)
  {
    Transform[] fishPath = PathManager.Instance.GetRandomPath();

    if (fishPath == null || fishPath.Length == 0)
    {
      Debug.LogError($"Invalid path");
      FishManager.Instance.DespawnFish(this);
      return;
    }

    transform.position = fishPath[0].position;

    // Flip sprite since your fish faces left and paths go left-to-right
    if (flipSpriteForLeftMovement)
    {
      transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
    }

    // Convert Transform[] to Vector3[] for DOPath
    Vector3[] path = new Vector3[fishPath.Length];
    for (int i = 0; i < fishPath.Length; i++)
    {
      path[i] = fishPath[i].position;
    }

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

  internal virtual void TakeDamage(float damage)
  {
    // To be overridden in child classes
  }

  internal virtual void Die()
  {
    FishManager.Instance.DespawnFish(this);
  }

  internal virtual void ResetFish()
  {
    movementTween?.Kill();
    transform.position = Vector3.zero;
    transform.rotation = Quaternion.identity;
    transform.localScale = Vector3.one;
  }
}
