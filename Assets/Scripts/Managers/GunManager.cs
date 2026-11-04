using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GunManager : MonoBehaviour
{
  private const float SeatMatchTolerance = 2f; // server px, when matching a shot origin to a seat

  internal static GunManager Instance;
  [SerializeField] private RectTransform bgRect;
  [SerializeField] private Camera worldCamera;
  [Tooltip("One rig per server seat, in seat order (index = seatIndex).")]
  [SerializeField] private SeatRig[] seatRigs;

  // Always the local player's rig, taken from its SeatRig on init. Null until then.
  internal BaseGun currentGun { get; private set; }
  internal BaseGun[] Guns { get; private set; }

  private Coroutine bulletFiringRoutine;
  private SeatRig localRig;

  void Awake()
  {
    Instance = this;
  }

  internal void UpdateAim(Vector3 screenPos)
  {
    if (currentGun == null)
      return;
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

    SeatRig.SetGunVisible(currentGun, false);

    if (localRig != null && localRig.gunSwitchAnimation)
      localRig.gunSwitchAnimation.StartAnimation();
    currentGun = newGun;

    SeatRig.SetGunVisible(currentGun, true);
  }

  internal void SwitchGun<T>() where T : BaseGun
  {
    if (Guns == null)
      return;

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

  // --------------------------------------------------------
  // SEATS
  // --------------------------------------------------------

  // game:init (and every re-init): place all rigs, take over ours, show the room's opponents.
  internal void SetupSeats(List<MLFGVec3> seats, int localSeat, Dictionary<string, int> roomSeats, string userId, string username)
  {
    if (seatRigs == null || seatRigs.Length == 0 || !Playfield.IsReady)
    {
      GameLog.Warn(LogCategory.Socket, "No seat rigs or playfield; seats not set up");
      return;
    }

    SetBulletFiring(false);
    localRig = null;

    for (int i = 0; i < seatRigs.Length; i++)
    {
      SeatRig rig = seatRigs[i];
      if (rig == null)
        continue;

      rig.Clear();
      if (seats != null && i < seats.Count && seats[i] != null)
        rig.PlaceAt(i, seats[i].ToVector2());
    }

    SeatRig mine = RigAt(localSeat, seats);
    if (mine != null)
    {
      mine.SetLocal(userId, username);
      TakeOver(mine);
    }
    else
    {
      GameLog.Error(LogCategory.Socket, $"No rig for local seat {localSeat}");
    }

    if (roomSeats != null)
    {
      foreach (var entry in roomSeats)
      {
        if (entry.Key != userId)
          OccupySeat(entry.Key, null, entry.Value);
      }
    }
  }

  private SeatRig RigAt(int seatIndex, List<MLFGVec3> seats)
  {
    if (seatIndex < 0 || seatIndex >= seatRigs.Length)
      return null;
    if (seats == null || seatIndex >= seats.Count || seats[seatIndex] == null)
      return null;
    return seatRigs[seatIndex];
  }

  private void TakeOver(SeatRig rig)
  {
    localRig = rig;
    Guns = rig.guns;
    currentGun = rig.defaultGun;
    UIManager.Instance.BindGunPanel(rig);
  }

  // game:init roster, game:player_joined, or an unknown shooter's first shot.
  internal void OccupySeat(string userId, string username, int seatIndex)
  {
    if (string.IsNullOrEmpty(userId) || seatIndex < 0 || seatIndex >= seatRigs.Length)
      return;

    SeatRig rig = seatRigs[seatIndex];
    if (rig == null || rig == localRig || rig.SeatIndex < 0)
      return;

    VacateSeat(userId); // a player can only hold one seat
    rig.SetOpponent(userId, username);
    GameLog.Info(LogCategory.Socket, $"Seat {seatIndex}: opponent {userId}");
  }

  internal void VacateSeat(string userId)
  {
    SeatRig rig = FindOpponentRig(userId);
    if (rig != null)
      rig.Clear();
  }

  private SeatRig FindOpponentRig(string userId)
  {
    foreach (var rig in seatRigs)
    {
      if (rig != null && rig != localRig && rig.OccupantId == userId)
        return rig;
    }
    return null;
  }

  // No join events yet, so an unknown shooter claims the seat their shot came from.
  private SeatRig ResolveOpponentRig(string shooterId, MLFGVec3 origin)
  {
    if (seatRigs == null || string.IsNullOrEmpty(shooterId))
      return null;

    SeatRig known = FindOpponentRig(shooterId);
    if (known != null)
      return known;

    if (origin != null)
    {
      Vector2 o = origin.ToVector2();
      foreach (var rig in seatRigs)
      {
        if (rig == null || rig == localRig || rig.SeatIndex < 0)
          continue;
        if ((rig.ServerSeat - o).sqrMagnitude <= SeatMatchTolerance * SeatMatchTolerance)
        {
          OccupySeat(shooterId, null, rig.SeatIndex);
          return rig;
        }
      }
    }

    GameLog.Warn(LogCategory.Bullet, SocketIOManager.Instance.WithInboundJson($"No seat for shooter {shooterId} at ({origin?.x},{origin?.y})"));
    return null;
  }

  internal void HandleRemoteFire(BulletFiredPayload fired)
  {
    SeatRig rig = ResolveOpponentRig(fired.shooterId, fired.origin);
    if (rig == null)
      return;

    if (GameLog.IsOn(LogCategory.Bullet))
      GameLog.Info(LogCategory.Bullet, SocketIOManager.Instance.WithInboundJson($"Opponent fire {fired.bulletId} by {fired.shooterId} seat {rig.SeatIndex} from ({fired.origin?.x:F0},{fired.origin?.y:F0}) angle {fired.angle:F1} bet #{fired.betIndex?.ToString() ?? "-"}"));
    ApplyOpponentBet(rig, fired.betIndex);
    if (fired.shooterBalance.HasValue)
      rig.SetBalance(fired.shooterBalance.Value);
    rig.PlayRemoteFire(fired);
  }

  internal void HandleRemoteShot(ShotResultPayload result)
  {
    SeatRig rig = ResolveOpponentRig(result.shooterId, result.origin);
    if (rig == null)
      return;

    // Aim comes from bullet_fired only: aimTarget is where the bullet ended up (after bounces),
    // not where the cannon pointed, so turning toward it swings the barrel sideways.
    if (GameLog.IsOn(LogCategory.Bullet))
      GameLog.Info(LogCategory.Bullet, SocketIOManager.Instance.WithInboundJson($"Opponent shot {result.shotId} by {result.shooterId} seat {rig.SeatIndex}: {result.outcome} at ({result.aimTarget?.x:F0},{result.aimTarget?.y:F0})"));
    ApplyOpponentBet(rig, result.betIndex);
    if (result.shooterBalance.HasValue)
      rig.SetBalance(result.shooterBalance.Value);
  }

  // betIndex points into the current hall's bets, which every player in the room shares.
  private static void ApplyOpponentBet(SeatRig rig, int? betIndex)
  {
    var bets = SocketIOManager.Instance?.bets;
    if (betIndex.HasValue && bets != null && betIndex.Value >= 0 && betIndex.Value < bets.Count)
      rig.SetBet(bets[betIndex.Value]);
  }
}
