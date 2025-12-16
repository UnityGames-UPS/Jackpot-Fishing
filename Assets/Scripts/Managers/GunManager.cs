using UnityEngine;
using System.Collections;

public class GunManager : MonoBehaviour
{
  [SerializeField] private BaseGun currentGun;
  [SerializeField] private RectTransform bgRect;
  [SerializeField] private Camera worldCamera;

  private Coroutine firingRoutine;

  internal void UpdateAim(Vector3 screenPos)
  {
    if (!RectTransformUtility.RectangleContainsScreenPoint(bgRect, screenPos, worldCamera))
      return;

    RectTransformUtility.ScreenPointToWorldPointInRectangle(
        bgRect, screenPos, worldCamera, out Vector3 worldPos);

    worldPos.z = 0;
    currentGun.UpdateAim(worldPos);
  }

  internal void SetFiring(bool firing)
  {
    if (firing)
    {
      if (firingRoutine == null)
        firingRoutine = StartCoroutine(FireLoop());
    }
    else
    {
      if (firingRoutine != null)
      {
        StopCoroutine(firingRoutine);
        firingRoutine = null;
      }
    }
  }

  private IEnumerator FireLoop()
  {
    while (true)
    {
      currentGun.Fire();
      yield return new WaitForSeconds(currentGun.FireInterval);
    }
  }
}
