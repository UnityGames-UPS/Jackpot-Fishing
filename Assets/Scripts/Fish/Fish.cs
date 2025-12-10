using UnityEngine;
using DG.Tweening;

internal class Fish : MonoBehaviour
{
  internal int FishId;
  protected Tween movementTween;

  internal virtual void Initialize(FishData data)
  {
    transform.position = data.fishPath[0].position;

    // Convert Transform[] to Vector3[] for DOPath
    Vector3[] path = new Vector3[data.fishPath.Length];
    for (int i = 0; i < data.fishPath.Length; i++)
    {
      path[i] = data.fishPath[i].position;
    }

    MoveAlongPath(path, data.minInterval / 1000f); // convert ms to seconds
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
            Color.clear
        )
        .SetEase(Ease.Linear)
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
  }
}
