using UnityEngine;
using DG.Tweening;

internal class SpecialFishRingView : MonoBehaviour
{
  [SerializeField] private Vector3 worldOffset = Vector3.zero;
  [SerializeField] private float rotationDuration = 1.2f;
  [SerializeField] private Ease rotationEase = Ease.Linear;

  private BaseFish targetFish;
  private bool isReturning;
  private Tween rotationTween;

  private void LateUpdate()
  {
    if (targetFish == null ||
        !targetFish.gameObject.activeInHierarchy)
    {
      StopRotation();
      return;
    }

    transform.position = targetFish.ColliderMidPoint + worldOffset;
  }

  private void OnDisable()
  {
    StopRotation();
    ClearTarget();
  }

  internal void AttachToFish(BaseFish fish, Vector3 offset)
  {
    targetFish = fish;
    worldOffset = offset;

    transform.position = fish.ColliderMidPoint + worldOffset;

    StartRotation();
  }

  internal void ReturnToPool()
  {
    if (isReturning)
      return;

    isReturning = true;
    StopRotation();
    ClearTarget();

    var pool = SpecialFishRingPool.Instance;
    if (pool != null)
      pool.ReturnToPool(this);
    else
      gameObject.SetActive(false);

    isReturning = false;
  }

  private void ClearTarget()
  {
    targetFish = null;
  }

  private void StartRotation()
  {
    if (rotationDuration <= 0f)
      return;

    rotationTween?.Kill();
    rotationTween = transform
      .DORotate(new Vector3(0f, 0f, 360f), rotationDuration, RotateMode.FastBeyond360)
      .SetEase(rotationEase)
      .SetLoops(-1, LoopType.Restart);
  }

  private void StopRotation()
  {
    rotationTween?.Kill();
    rotationTween = null;
    transform.localRotation = Quaternion.identity;
  }
}
