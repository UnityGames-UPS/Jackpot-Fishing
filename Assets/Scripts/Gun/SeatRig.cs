using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

// One of the room's cannon rigs (one per server seat). Placed at gameData.seats[i].x, keeping
// the gap to the screen edge it was laid out with in the scene. Top seats (y = 0) are turned
// 180° so the rig faces down.
public class SeatRig : MonoBehaviour
{
  [Header("Guns")]
  [SerializeField] internal BaseGun[] guns;
  [SerializeField] internal BaseGun defaultGun;
  [SerializeField] internal ImageAnimation gunSwitchAnimation;

  [Header("Gun Panel")]
  [Tooltip("Bet/balance panel. Shown on every occupied rig; it fills the gap between the cannon and the screen edge.")]
  [FormerlySerializedAs("localOnlyUI")]
  [SerializeField] private GameObject gunPanel;
  [SerializeField] internal Button plusBetButton;
  [SerializeField] internal Button minusBetButton;
  [SerializeField] internal TMP_Text betText;
  [SerializeField] internal TMP_Text balanceText;
  [SerializeField] private TMP_Text nameText;

  internal int SeatIndex { get; private set; } = -1;
  internal string OccupantId { get; private set; }
  internal bool IsLocal { get; private set; }
  internal bool IsOccupied => IsLocal || !string.IsNullOrEmpty(OccupantId);
  internal Vector2 ServerSeat { get; private set; }

  // Seats sit on the screen edge, but the rig's pivot is its center. Keep the gap to the edge
  // the rig was laid out with in the scene, measured once before the first placement.
  private float? edgeInset;

  internal void PlaceAt(int seatIndex, Vector2 serverSeat)
  {
    SeatIndex = seatIndex;
    ServerSeat = serverSeat;

    if (edgeInset == null)
    {
      float designedY = Playfield.WorldToServer(transform.position).y;
      edgeInset = Mathf.Min(designedY, Playfield.Height - designedY);
    }

    bool top = serverSeat.y < Playfield.Height * 0.5f;
    Vector2 placed = serverSeat + new Vector2(0f, top ? edgeInset.Value : -edgeInset.Value);
    Vector3 world = Playfield.ServerToWorld(placed);
    transform.position = new Vector3(world.x, world.y, transform.position.z);
    transform.localRotation = top ? Quaternion.Euler(0, 0, 180f) : Quaternion.identity;
  }

  internal void SetLocal(string userId, string username)
  {
    IsLocal = true;
    OccupantId = userId;
    Show(username, true);
  }

  internal void SetOpponent(string userId, string username)
  {
    IsLocal = false;
    OccupantId = userId;
    Show(username, false);
  }

  internal void Clear()
  {
    IsLocal = false;
    OccupantId = null;
    gameObject.SetActive(false);
  }

  private void Show(string username, bool local)
  {
    gameObject.SetActive(true);
    if (gunPanel) gunPanel.SetActive(true);
    if (nameText) nameText.text = username ?? "";
    // Only the local player changes their bet. A rig can switch roles across inits, so set both ways.
    if (plusBetButton) plusBetButton.gameObject.SetActive(local);
    if (minusBetButton) minusBetButton.gameObject.SetActive(local);
    if (!local)
    {
      // No opponent info until their first shot: balance 0, the hall's lowest bet.
      if (balanceText) balanceText.text = 0.0.ToString("N2");
      var bets = SocketIOManager.Instance?.bets;
      SetBet(bets != null && bets.Count > 0 ? bets[0] : 0f);
      // Opponents' bet buttons do nothing (they may still hold UIManager's default listeners).
      if (plusBetButton) plusBetButton.onClick.RemoveAllListeners();
      if (minusBetButton) minusBetButton.onClick.RemoveAllListeners();
    }
    ShowOnly(defaultGun);
  }

  private void ShowOnly(BaseGun visible)
  {
    if (guns == null)
      return;
    foreach (var gun in guns)
      SetGunVisible(gun, gun == visible);
  }

  // The lazer gun's visuals live on its parent, so toggle that instead.
  internal static void SetGunVisible(BaseGun gun, bool visible)
  {
    if (gun == null)
      return;
    if (gun is LazerGun)
      gun.transform.parent.gameObject.SetActive(visible);
    else
      gun.gameObject.SetActive(visible);
  }

  // Opponent balance from their shots (ours comes from UIManager).
  internal void SetBalance(double balance)
  {
    if (balanceText && !IsLocal)
      balanceText.text = balance.ToString("N2");
  }

  // Opponent bet from the betIndex of their shots (ours comes from UIManager).
  internal void SetBet(float bet)
  {
    if (betText && !IsLocal)
      betText.text = bet.ToString("N2");
  }

  // Opponent shots are visual only. The rig switches instantly (no switch animation) to the gun
  // they fired; torpedo and laser aim at targetFishId.
  internal void PlayRemoteFire(BulletFiredPayload fired)
  {
    BaseFish target = null;
    FishManager.Instance?.TryGetFish(fired.targetFishId, out target);

    switch (fired.weaponType)
    {
      case MLFGWeaponIds.Torpedo when GunOf<TorpedoGun>() is TorpedoGun torpedoGun:
        ShowOnly(torpedoGun);
        torpedoGun.PlayRemoteShot(target, fired.angle);
        break;
      case MLFGWeaponIds.Laser when GunOf<LazerGun>() is LazerGun lazerGun:
        ShowOnly(lazerGun);
        lazerGun.PlayRemoteTick(target);
        break;
      default:
        ShowOnly(defaultGun);
        if (defaultGun is SimpleGun simpleGun)
          simpleGun.PlayRemoteShot(fired.bulletId, fired.weaponType, fired.angle, fired.bulletSpeed, fired.firedAt);
        break;
    }
  }

  // Opponent laser tick, from their shot_result.
  internal void PlayRemoteLaser(string fishId)
  {
    if (GunOf<LazerGun>() is not LazerGun lazerGun)
      return;

    BaseFish target = null;
    FishManager.Instance?.TryGetFish(fishId, out target);
    ShowOnly(lazerGun);
    lazerGun.PlayRemoteTick(target);
  }

  internal void StopRemoteLaser()
  {
    GunOf<LazerGun>()?.StopRemote();
  }

  private T GunOf<T>() where T : BaseGun
  {
    if (guns == null)
      return null;
    foreach (var gun in guns)
    {
      if (gun is T match)
        return match;
    }
    return null;
  }
}
