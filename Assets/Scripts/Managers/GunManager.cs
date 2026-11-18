using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GunManager : MonoBehaviour
{
  private const float SeatMatchTolerance = 2f; // server px, when matching a shot origin to a seat

  internal static GunManager Instance;
  [SerializeField] private RectTransform bgRect;
  [SerializeField] private Camera worldCamera;

  [Header("Seats")]
  [Tooltip("A rig laid out in the scene and left inactive. Every seat is a clone of it, made under the same parent.")]
  [SerializeField] private SeatRig seatRigTemplate;
  [Tooltip("Rigs cloned on Awake. More are made if an init has more seats.")]
  [SerializeField] private int prewarmSeats = 4;

  // Pooled rigs, index = seatIndex. Rigs past the room's seat count stay inactive.
  private readonly List<SeatRig> seatRigs = new();

  // Always the local player's rig, taken from its SeatRig on init. Null until then.
  internal BaseGun currentGun { get; private set; }
  internal BaseGun[] Guns { get; private set; }

  private Coroutine bulletFiringRoutine;
  private SeatRig localRig;

  void Awake()
  {
    Instance = this;
    if (seatRigTemplate != null)
      seatRigTemplate.gameObject.SetActive(false);
    EnsureRigs(prewarmSeats);
  }

  private void EnsureRigs(int count)
  {
    if (seatRigTemplate == null)
      return;

    while (seatRigs.Count < count)
    {
      // Same parent and local layout as the template, so PlaceAt keeps its edge gap.
      SeatRig rig = Instantiate(seatRigTemplate, seatRigTemplate.transform.parent, false);
      rig.name = $"GunRig (seat {seatRigs.Count})";
      rig.gameObject.SetActive(false);
      seatRigs.Add(rig);
    }
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
  internal void SetupSeats(List<MLFGVec3> seats, int localSeat, List<MLFGRoomPlayer> players, Dictionary<string, int> roomSeats, string userId, string username)
  {
    if (seatRigTemplate == null || !Playfield.IsReady)
    {
      GameLog.Warn(LogCategory.Socket, "No seat rig template or playfield; seats not set up");
      return;
    }

    SetBulletFiring(false);
    localRig = null;

    // One rig per seat of this room; the rest go back to the pool.
    int seatCount = seats?.Count ?? 0;
    EnsureRigs(seatCount);
    for (int i = 0; i < seatRigs.Count; i++)
    {
      SeatRig rig = seatRigs[i];
      if (i < seatCount && seats[i] != null)
      {
        rig.Clear();
        rig.PlaceAt(i, seats[i].ToVector2());
      }
      else
      {
        rig.Retire();
      }
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

    // players has names, balances and bets; roomSeats (ids only) is the fallback.
    if (players != null && players.Count > 0)
    {
      foreach (var player in players)
      {
        if (player == null || player.userId == userId)
          continue;

        OccupySeat(player.userId, player.username, player.seatIndex, player.balance, player.betIndex);
      }
    }
    else if (roomSeats != null)
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
    if (seatIndex < 0 || seatIndex >= seatRigs.Count)
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
    // The bet was reset before the seats were set up, so the new rig picks up its tier here.
    rig.NormalGun?.SetTier(GunTiers.ForBet(UIManager.Instance.BetCounter));
  }

  // game:init roster, game:player_joined, or an unknown shooter's first shot.
  internal void OccupySeat(string userId, string username, int seatIndex, double? balance = null, int? betIndex = null)
  {
    if (string.IsNullOrEmpty(userId) || seatIndex < 0 || seatIndex >= seatRigs.Count)
      return;

    SeatRig rig = seatRigs[seatIndex];
    if (rig == null || rig == localRig || rig.SeatIndex < 0)
      return;

    // Re-showing the rig resets its balance to 0, so only set it up when the occupant changes
    // (e.g. a join that repeats the init roster, or a shot that already claimed the seat).
    if (rig.OccupantId != userId || !rig.gameObject.activeSelf)
    {
      VacateSeat(userId); // a player can only hold one seat
      rig.SetOpponent(userId, username);
      GameLog.Info(LogCategory.Socket, $"Seat {seatIndex}: opponent {userId}");
    }

    if (balance.HasValue)
      rig.SetBalance(balance.Value);
    ApplyOpponentBet(rig, betIndex);
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
    if (string.IsNullOrEmpty(shooterId))
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
    double? balance = fired.ShooterBalance;
    if (balance.HasValue)
      rig.SetBalance(balance.Value);
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
    double? balance = result.ShooterBalance;
    if (balance.HasValue)
      rig.SetBalance(balance.Value);

    // The laser sends no FIRE (so no bullet_fired): each tick's shot_result draws the beam.
    if (result.weaponType == MLFGWeaponIds.Laser)
      rig.PlayRemoteLaser(string.IsNullOrEmpty(result.targetFishId) ? result.fishId : result.targetFishId);
  }

  // game:laser_stopped for an opponent.
  internal void StopRemoteLaser(string shooterId)
  {
    FindOpponentRig(shooterId)?.StopRemoteLaser();
  }

  // betIndex points into the current hall's bets, which every player in the room shares.
  private static void ApplyOpponentBet(SeatRig rig, int? betIndex)
  {
    if (betIndex.HasValue)
      rig.SetBetIndex(betIndex.Value);
  }

  // Every local bet change (UIManager.OnBetChanged): the normal gun's tier art follows the bet,
  // and the switch transition plays only while the normal gun is out.
  internal void ApplyLocalBet(int betIndex)
  {
    if (localRig == null)
      return;

    localRig.NormalGun?.SetTier(GunTiers.ForBet(betIndex));
    if (currentGun is SimpleGun && localRig.gunSwitchAnimation)
      localRig.gunSwitchAnimation.RestartAnimation();
  }
}
