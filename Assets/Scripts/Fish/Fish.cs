using UnityEngine;
using DG.Tweening;

internal class Fish : MonoBehaviour
{
  internal int FishId;
  protected Tween movementTween;

  internal virtual void Initialize(FishData data)
  {
    transform.position = data.fishPath[0].position;
    MoveAlongPath(, data.minInterval);
  }

  internal virtual void MoveAlongPath(Vector3[] path, float duration)
  {
    movementTween?.Kill();
    movementTween = transform.DOPath(path, duration, PathType.CatmullRom, PathMode.Sidescroller2D, 10, Color.black)
        .SetEase(Ease.Linear)
        .OnComplete(() => FishManager.Instance.DespawnFish(this));
  }

  internal virtual void TakeDamage(float damage)
  {

  }

  internal virtual void Die()
  {
    // play effect, reward coins, etc.
    FishManager.Instance.DespawnFish(this);
  }

  internal virtual void ResetFish()
  {
    movementTween?.Kill();
    transform.position = Vector3.zero;
  }
}
