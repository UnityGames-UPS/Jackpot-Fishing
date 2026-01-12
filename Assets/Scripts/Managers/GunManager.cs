using UnityEngine;
using System.Collections;

public class GunManager : MonoBehaviour
{
  internal static GunManager Instance;
  [SerializeField] internal BaseGun currentGun;
  [SerializeField] internal BaseGun[] Guns;
  [SerializeField] private RectTransform bgRect;
  [SerializeField] private Camera worldCamera;
  [SerializeField] private ImageAnimation GunSwitchAnimation;

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
      if (currentGun is SimpleGun SimpleGun)
      {
        SimpleGun.TryFire();
      }
      yield return null;
    }
  }

  private void SwitchGun(BaseGun newGun)
  {
    if (newGun == null || newGun == currentGun)
      return;

    SetBulletFiring(false);

    if (currentGun != null)
    {
      if (currentGun is LazerGun)
        currentGun.transform.parent.gameObject.SetActive(false);
      else
        currentGun.gameObject.SetActive(false);
    }

    GunSwitchAnimation.StartAnimation();
    currentGun = newGun;

    if (currentGun is LazerGun)
      currentGun.transform.parent.gameObject.SetActive(true);
    else
      currentGun.gameObject.SetActive(true);
  }

  internal void SwitchGun<T>() where T : BaseGun
  {
    foreach (var gun in Guns)
    {
      if (gun is T)
      {
        SwitchGun(gun);
        return;
      }
    }

    Debug.LogError($"Gun of type {typeof(T)} not found");
  }

  internal void ForceStopTorpedoFire(BaseFish killedFish)
  {
    if (currentGun is TorpedoGun torpedoGun)
    {
      torpedoGun.OnFishKilled(killedFish);
    }
  }

}
