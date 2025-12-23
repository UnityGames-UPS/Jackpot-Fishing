using UnityEngine;
using System.Collections;

public class GunManager : MonoBehaviour
{
  internal static GunManager Instance;
  [SerializeField] internal BaseGun currentGun;
  [SerializeField] private RectTransform bgRect;
  [SerializeField] private Camera worldCamera;

  private Coroutine bulletFiringRoutine;

  void Awake()
  {
    Instance = this;
  }

  internal void UpdateAim(Vector3 screenPos)
  {
    if (!RectTransformUtility.RectangleContainsScreenPoint(bgRect, screenPos, worldCamera))
      return;

    RectTransformUtility.ScreenPointToWorldPointInRectangle(
        bgRect, screenPos, worldCamera, out Vector3 worldPos);

    worldPos.z = 0;
    currentGun.UpdateAim(worldPos);
  }

  internal void SetBulletFiring(bool bulletFiring)
  {
    if (bulletFiring)
    {
      if (bulletFiringRoutine == null)
        bulletFiringRoutine = StartCoroutine(BulletFireLoop());
    }
    else
    {
      if (bulletFiringRoutine != null)
      {
        StopCoroutine(bulletFiringRoutine);
        bulletFiringRoutine = null;
      }
    }
  }

  private IEnumerator BulletFireLoop()
  {
    while (true)
    {
      if(currentGun is SimpleGun)
      {
        currentGun.Fire();
        yield return new WaitForSeconds(currentGun.FireInterval);
      }
    }
  }
}
