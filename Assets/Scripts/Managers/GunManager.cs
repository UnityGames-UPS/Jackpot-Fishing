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
  [Tooltip("Moves horizontally to the player's seat on init. Defaults to the parent of the first gun.")]
  [SerializeField] private Transform seatRoot;

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

    GameLog.Error(LogCategory.Bullet, $"Gun of type {typeof(T)} not found");
  }

  internal void ForceStopTorpedoFire(BaseFish killedFish)
  {
    if (currentGun is TorpedoGun torpedoGun)
    {
      torpedoGun.OnFishKilled(killedFish);
    }
  }


  // Slides the cannon (and the bet UI with it) along its own horizontal axis to the server seat.
  // Bottom seats only for now; top seats (y = 0) would also need the cannon flipped.
  internal void PlaceAtSeat(Vector2 serverSeat)
  {
    Transform root = seatRoot != null ? seatRoot : (Guns != null && Guns.Length > 0 ? Guns[0].transform.parent : null);
    if (root == null || root.parent == null || !Playfield.IsReady)
      return;

    Vector3 seatLocal = root.parent.InverseTransformPoint(Playfield.ServerToWorld(serverSeat));
    Vector3 current = root.localPosition;
    root.localPosition = new Vector3(seatLocal.x, current.y, current.z);
  }
}
