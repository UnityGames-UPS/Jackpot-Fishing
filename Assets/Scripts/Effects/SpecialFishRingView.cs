using UnityEngine;
internal class SpecialFishRingView : MonoBehaviour
{
  [SerializeField] private float rotationDuration = 1.2f;

  private BaseFish targetFish;
  private bool isReturning;
  private float spinAngle;

  private void LateUpdate()
  {
    if (targetFish == null ||
        !targetFish.gameObject.activeInHierarchy)
    {
      StopRotation();
      return;
    }

    transform.position = targetFish.ColliderMidPoint;
    spinAngle = GetNextSpinAngle();
    transform.localRotation = Quaternion.Euler(0f, 0f, GetOrientationZ() + spinAngle);
  }

  private void OnDisable()
  {
    StopRotation();
    ClearTarget();
  }

  internal void AttachToFish(BaseFish fish)
  {
    targetFish = fish;

    transform.position = targetFish.ColliderMidPoint;
    transform.localRotation = Quaternion.Euler(0f, 0f, GetOrientationZ() + spinAngle);

    StartRotation();
  }

  internal void ReturnToPool()
  {
    if (isReturning)
      return;
    isReturning = true;

    var pool = SpecialFishRingPool.Instance;
    if (pool != null)
      pool.ReturnToPool(this);
    else
      gameObject.SetActive(false);
    
    StopRotation();
    ClearTarget();

    isReturning = false;
  }

  private void ClearTarget()
  {
    targetFish = null;
  }

  private void StartRotation()
  {
    // DOTween rotation disabled; LateUpdate handles spin.
    spinAngle = 0f;
  }

  private void StopRotation()
  {
    // DOTween rotation disabled; LateUpdate handles spin.
    spinAngle = 0f;
    transform.localRotation = Quaternion.Euler(0f, 0f, GetOrientationZ());
  }

  private float GetNextSpinAngle()
  {
    if (rotationDuration <= 0f)
      return spinAngle;

    float degreesPerSecond = 360f / rotationDuration;
    float nextAngle = spinAngle + (degreesPerSecond * Time.deltaTime);
    if (nextAngle >= 360f)
      nextAngle -= 360f;

    return nextAngle;
  }

  private float GetOrientationZ()
  {
    OrientationChange orientation = OrientationChange.Instance;
    if (orientation != null && !orientation.IsLandscape)
      return 90f;

    return 0f;
  }
}
