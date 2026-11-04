using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Best.SocketIO;
using Best.SocketIO.Events;
using Best.SocketIO.Parsers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DG.Tweening;

// Networking for the ML-FG (server-authoritative) fish backend. Wire contract and DTOs:
// Scripts/Network/MLFGDtos.cs. The GameObject must stay named "SocketManager" (the JS bridge
// calls ReceiveAuthToken on it).
public class SocketIOManager : MonoBehaviour
{
  internal static SocketIOManager Instance;
  [SerializeField] private GameObject blocker;
  private SocketManager MainSocketManager;
  private Socket MainGameSocket;
  [SerializeField] internal JSFunctCalls JSManager;
  [SerializeField] protected string TestSocketURI = "https://devrealtime.dingdinghouse.com/";
  protected string SocketURI = null;
  [SerializeField] private string testToken;

  [Header("ML-FG")]
  [SerializeField] private float requestAckTimeout = 5f;
  [SerializeField] private float clockSyncWait = 2f; // max wait for the first clock sync before processing fish events

  [Header("Paused features (old backend)")]
  [SerializeField] internal List<float> bets = new();
  [SerializeField] internal List<InitFishGroup> initFishes;

  // Bet multipliers per gun (0: normal 1: torpedo 2: laser), from init's weapons.*.cost.
  // Defaults apply until init sends a cost for that weapon.
  private static readonly float[] DefaultGunCosts = { 1f, 4f, 1f };
  internal List<float> GunCosts { get; } = new(DefaultGunCosts);

  protected string gameNamespace = "playground-multiplayer";
  private string myAuth = null;
  internal bool isLoaded = false;

  // Heartbeat: time_sync doubles as the ping; its ack (or a pong) is the pong.
  private bool hasEverConnected = false;
  private float pingInterval = 2f;
  private const int SyncBurstCount = 5;
  private const float SyncBurstInterval = 0.2f;
  private bool waitingForPong = false;
  private int missedPongs = 0;
  private const int MaxMissedPongs = 15;
  private Coroutine PingRoutine;

  // Background timeout (JS visibility bridge only)
  private bool hasFocus = true;
  private float focusLostTime = 0f;
  private Coroutine focusCheckRoutine;
  private float maxBackgroundTime = 60f;
  private bool isExiting = false;
  private bool isBeingDestroyed = false;

  // Session state from game:init / room:joined
  internal string UserId { get; private set; }
  internal string RoomId { get; private set; }
  internal int SeatIndex { get; private set; } = -1;
  internal string NormalWeaponId { get; private set; } = "normal";
  private int defaultMaxBounces = 3;
  private Dictionary<string, MLFGWeapon> weapons;
  internal bool IsInitialized { get; private set; }
  private List<MLFGVec3> seats;

  // Fish events wait here (in order) until the clock is synced, so spawns and their
  // despawns/kills are never applied out of order or against an unsynced clock.
  private readonly Queue<Action> waitingForClock = new();
  private float connectedAt;
  private string inboundJson; // raw JSON of the event being handled, for logs

  // After the server's "Too many requests" error, every gun holds fire so the limit can reset.
  // The server keeps refusing for a while once tripped, so the pause doubles on each repeat
  // (1.5, 3, 6, 12 s) and resets once a request is answered again.
  private const float RequestLimitBackoff = 1.5f;
  private const float MaxRequestLimitBackoff = 12f;
  private int requestLimitStrikes;
  private float shotsPausedUntil;

  // Recent `request` emits, so the request-limit error can say what we actually sent.
  private const float RequestHistorySeconds = 5f;
  private readonly Queue<(float time, string type)> recentRequests = new();

  internal bool IsConnected => MainGameSocket != null && MainGameSocket.IsOpen;
  internal bool CanShoot =>
    IsConnected && IsInitialized && ServerClock.IsSynced && !string.IsNullOrEmpty(RoomId) &&
    Time.unscaledTime >= shotsPausedUntil;

  internal Vector2 SeatOrigin =>
    seats != null && SeatIndex >= 0 && SeatIndex < seats.Count
      ? seats[SeatIndex].ToVector2()
      : new Vector2(Playfield.Width * 0.5f, Playfield.Height);

  internal bool IsOwnShot(string shooterId) =>
    string.IsNullOrEmpty(UserId) || shooterId == UserId;

  private void Awake()
  {
    Instance = this;
    Application.runInBackground = true;
    DOTween.Init();
    DOTween.defaultTimeScaleIndependent = true;
    blocker.SetActive(true);
    isLoaded = false;
  }

  private void Start()
  {
    OpenSocket();
  }

  private void Update()
  {
    ServerClock.Tick(Time.unscaledDeltaTime);

    if (waitingForClock.Count > 0 &&
        (ServerClock.IsSynced || Time.unscaledTime - connectedAt > clockSyncWait))
    {
      while (waitingForClock.Count > 0)
        waitingForClock.Dequeue()?.Invoke();
    }
  }

  private void OnDestroy()
  {
    isBeingDestroyed = true;
  }

  void ReceiveAuthToken(string jsonData)
  {
    var data = JsonUtility.FromJson<AuthTokenData>(jsonData);
    GameLog.Info(LogCategory.Lifecycle, "Received auth data for " + data?.socketURL); // never log the token
    SocketURI = data.socketURL;
    myAuth = data.cookie;
  }

  // --------------------------------------------------------
  // CONNECTION
  // --------------------------------------------------------

  private void OpenSocket()
  {
    SocketOptions options = new SocketOptions();
    options.AutoConnect = false;
    options.Reconnection = false;
    options.Timeout = TimeSpan.FromSeconds(3);
    options.ConnectWith = Best.SocketIO.Transports.TransportTypes.WebSocket;

#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("authToken");
    StartCoroutine(WaitForAuthToken(options));
#else
    options.Auth = (manager, socket) => new { token = testToken };
    SetupGameSocketManager(options);
#endif
  }

  private IEnumerator WaitForAuthToken(SocketOptions options)
  {
    while (myAuth == null || SocketURI == null)
      yield return null;

    options.Auth = (manager, socket) => new { token = myAuth };
    SetupGameSocketManager(options);
  }

  private void SetupGameSocketManager(SocketOptions options)
  {
#if UNITY_EDITOR
    Uri uri = new Uri(TestSocketURI);
#else
    Uri uri = new Uri(SocketURI);
#endif
    // JSON.NET parser so every handler can take a JToken: the server sends some payloads as
    // objects and some as JSON strings.
    MainSocketManager = new SocketManager(uri, new JsonDotNetParser(), options);
    MainGameSocket = string.IsNullOrWhiteSpace(gameNamespace)
      ? MainSocketManager.Socket
      : MainSocketManager.GetSocket("/" + gameNamespace);

    MainGameSocket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
    MainGameSocket.On(SocketIOEventTypes.Disconnect, OnDisconnected);
    MainGameSocket.On<Error>(SocketIOEventTypes.Error, OnError);

    Subscribe(MLFGEvents.TimeSyncAck, OnTimeSyncAck);
    Subscribe(MLFGEvents.Pong, _ => OnPongReceived());
    Subscribe(MLFGEvents.RoomJoined, OnRoomJoined);
    Subscribe(MLFGEvents.Init, OnInit);
    Subscribe(MLFGEvents.BalanceSync, OnBalanceSync);
    Subscribe(MLFGEvents.FishSpawned, p => RunWhenClockReady(() => OnFishSpawned(p)));
    Subscribe(MLFGEvents.FishDespawned, p => RunWhenClockReady(() => OnFishDespawned(p)));
    Subscribe(MLFGEvents.ShotResult, p => RunWhenClockReady(() => OnShotResult(p)));
    Subscribe(MLFGEvents.BulletFired, OnBulletFired);
    Subscribe(MLFGEvents.FormationAlert, OnFormationAlert);
    Subscribe(MLFGEvents.PlayerJoined, OnPlayerJoined);
    Subscribe(MLFGEvents.PlayerLeft, OnPlayerLeft);

    MainSocketManager.Open();
  }

  private void Subscribe(string eventName, Action<JObject> handler)
  {
    MainGameSocket.On<JToken>(eventName, token =>
    {
      inboundJson = RawJson(token);

      JObject payload = ToPayload(token);
      if (payload == null)
      {
        GameLog.Warn(LogCategory.Socket, WithJson($"⬇ {eventName}: unreadable payload", inboundJson));
        return;
      }

      // Heartbeat acks are logged under Clock, not Payload, or they'd drown everything else.
      if (eventName != MLFGEvents.TimeSyncAck && eventName != MLFGEvents.Pong && GameLog.IsOn(LogCategory.Payload))
        GameLog.Info(LogCategory.Payload, WithJson($"⬇ {eventName}", inboundJson));

      try
      {
        handler(payload);
      }
      catch (Exception e)
      {
        GameLog.Error(LogCategory.Socket, WithJson($"⬇ {eventName} handler failed: {e}", inboundJson));
      }
      finally
      {
        inboundJson = null;
      }
    });
  }

  // The JSON exactly as the server sent it (string payloads unquoted).
  private static string RawJson(JToken token)
  {
    if (token == null)
      return "null";
    return token.Type == JTokenType.String ? (string)token : token.ToString(Formatting.None);
  }

  // Summary first, then the raw JSON on its own line (full text in the console's detail pane).
  private static string WithJson(string summary, string json) => summary + "\n" + json;

  // Accepts an object or a JSON string, and unwraps an optional { payload: {...} } envelope.
  private static JObject ToPayload(JToken token)
  {
    if (token == null)
      return null;

    if (token.Type == JTokenType.String)
    {
      try { token = JToken.Parse((string)token); }
      catch (JsonException) { return null; }
    }

    if (token is not JObject obj)
      return null;

    return obj["payload"] is JObject inner ? inner : obj;
  }

  private void RunWhenClockReady(Action action)
  {
    if (waitingForClock.Count == 0 && ServerClock.IsSynced)
    {
      action();
      return;
    }

    // Queued events still log the JSON they arrived with.
    string json = inboundJson;
    waitingForClock.Enqueue(() =>
    {
      inboundJson = json;
      try { action(); }
      finally { inboundJson = null; }
    });
  }

  // Appends the JSON of the socket event being handled, if any (logs from other managers).
  internal string WithInboundJson(string summary) =>
    string.IsNullOrEmpty(inboundJson) ? summary : WithJson(summary, inboundJson);

  void OnConnected(ConnectResponse resp)
  {
    GameLog.Info(LogCategory.Socket, "✅ Connected to /" + gameNamespace);

    if (hasEverConnected)
      UIManager.Instance.CheckAndClosePopups();

    hasEverConnected = true;
    connectedAt = Time.unscaledTime;
    waitingForPong = false;
    missedPongs = 0;
    ServerClock.Reset();
    SendPing();
  }

  private void OnDisconnected()
  {
    GameLog.Warn(LogCategory.Socket, "⚠️ Disconnected from server.");
    IsInitialized = false;
    UIManager.Instance.DisconnectionPopup();
    ResetPingRoutine();
  }

  private void OnError(Error err)
  {
    bool requestLimit = !string.IsNullOrEmpty(err?.message) && err.message.Contains("Too many request");
    string summary = "Socket error: " + err + (requestLimit ? "\n" + RecentRequestSummary() : "");
    GameLog.Error(LogCategory.Socket, WithJson(summary, JsonConvert.SerializeObject(err)));
    if (requestLimit)
    {
      float pause = Mathf.Min(RequestLimitBackoff * Mathf.Pow(2f, requestLimitStrikes), MaxRequestLimitBackoff);
      requestLimitStrikes++;
      shotsPausedUntil = Time.unscaledTime + pause;
      GameLog.Warn(LogCategory.Socket, $"Request limit hit #{requestLimitStrikes}: guns paused for {pause:F1} s");
      return;
    }

    if (!string.IsNullOrEmpty(err?.message) && err.message.Contains("Session expired"))
    {
      GameLog.Warn(LogCategory.Socket, "Session expired detected");
      OnDisconnected();
#if UNITY_WEBGL && !UNITY_EDITOR
      JSManager.SendCustomMessage("session_expired");
#endif
    }
    else
    {
#if UNITY_WEBGL && !UNITY_EDITOR
      JSManager.SendCustomMessage("error");
#endif
    }
  }

  // --------------------------------------------------------
  // CLOCK SYNC + HEARTBEAT
  // --------------------------------------------------------

  private void OnTimeSyncAck(JObject payload)
  {
    OnPongReceived();

    var ack = payload.ToObject<TimeSyncAckPayload>();
    if (ack == null || ack.clientTime <= 0)
      return;

    bool wasSynced = ServerClock.IsSynced;
    ServerClock.AddSample(ack.clientTime, ack.serverTime);

    if (!wasSynced && ServerClock.IsSynced)
      GameLog.Info(LogCategory.Clock, WithJson($"Clock synced: offset {ServerClock.OffsetMs:F0} ms, rtt {ServerClock.LastRttMs:F0} ms", inboundJson));
    else if (GameLog.IsOn(LogCategory.Clock))
      GameLog.Info(LogCategory.Clock, WithJson($"time_sync rtt {ServerClock.LastRttMs:F0} ms, offset {ServerClock.OffsetMs:F0} ms", inboundJson));
  }

  private void OnPongReceived()
  {
    waitingForPong = false;
    missedPongs = 0;
  }

  private void SendTimeSync()
  {
    if (IsConnected)
      MainGameSocket.Emit(MLFGEvents.TimeSync, (long)ServerClock.LocalNowMs);
  }

  private void SendPing()
  {
    ResetPingRoutine();
    PingRoutine = StartCoroutine(PingCheck());
  }

  void ResetPingRoutine()
  {
    if (PingRoutine != null)
      StopCoroutine(PingRoutine);
    PingRoutine = null;
  }

  private IEnumerator PingCheck()
  {
    // Quick burst for an accurate first offset, then the regular heartbeat.
    for (int i = 0; i < SyncBurstCount; i++)
    {
      SendTimeSync();
      yield return new WaitForSecondsRealtime(SyncBurstInterval);
    }

    while (true)
    {
      if (missedPongs == 0)
      {
        UIManager.Instance.CheckAndClosePopups();
      }

      if (waitingForPong)
      {
        if (missedPongs == 2)
        {
          UIManager.Instance.ReconnectionPopup();
        }
        missedPongs++;
        GameLog.Warn(LogCategory.Socket, $"⚠️ Pong missed #{missedPongs}/{MaxMissedPongs}");

        if (missedPongs >= MaxMissedPongs)
        {
          GameLog.Error(LogCategory.Socket, $"❌ Unable to connect to server — {MaxMissedPongs} consecutive pongs missed.");
          IsInitialized = false;
          UIManager.Instance.DisconnectionPopup();
          yield break;
        }
      }

      waitingForPong = true;
      SendTimeSync();
      yield return new WaitForSecondsRealtime(pingInterval);
    }
  }

  // --------------------------------------------------------
  // INBOUND GAME EVENTS
  // --------------------------------------------------------

  // Broadcast to the room on every join (ours and other players'). game:init also carries roomId.
  private void OnRoomJoined(JObject payload)
  {
    string roomId = payload.Value<string>("roomId");
    string userId = payload.Value<string>("userId");
    bool own = string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(UserId) || userId == UserId;
    if (own && !string.IsNullOrEmpty(roomId))
      RoomId = roomId;
  }

  private void OnInit(JObject payload)
  {
    var init = payload.ToObject<MLFGInitData>();
    if (init == null)
      return;

    if (!string.IsNullOrEmpty(init.roomId))
      RoomId = init.roomId;
    UserId = init.player?.userId;
    SeatIndex = init.seatIndex;
    seats = init.gameData?.seats;
    defaultMaxBounces = init.gameData?.maxBulletBounces ?? 3;
    ApplyWeapons(init.gameData);

    bets = init.gameData?.bets != null && init.gameData.bets.Count > 0
      ? init.gameData.bets
      : new List<float> { 1f };

    UIManager.Instance.ResetBalance(init.player?.balance ?? 0);
    UIManager.Instance.HandeGameInit();
    GunManager.Instance?.SetupSeats(seats, SeatIndex, init.roomSeats, UserId, init.player?.username);

    // A re-init (reconnect) starts from the server's fish list, not ours.
    var activeFish = init.activeFish;
    RunWhenClockReady(() =>
    {
      FishManager.Instance.ClearAllFish();
      if (activeFish != null)
      {
        foreach (var fish in activeFish)
          FishManager.Instance.SpawnFromServer(fish);
      }
    });

    GameLog.Info(LogCategory.Socket, WithJson(
      $"Init: room {RoomId}, seat {SeatIndex}, user {UserId}, weapon '{NormalWeaponId}', " +
      $"maxBounces {BouncesFor(NormalWeaponId)}, bulletSpeed {BulletSpeedFor(NormalWeaponId)?.ToString() ?? "default"}, " +
      $"fire intervals (with margin) normal {FireIntervalFor(NormalWeaponId, 0f) * 1000f:F0} / torpedo {FireIntervalFor(MLFGWeaponIds.Torpedo, 0f) * 1000f:F0} / laser {FireIntervalFor(MLFGWeaponIds.Laser, 0f) * 1000f:F0} ms, balance {init.player?.balance}, activeFish {activeFish?.Count ?? 0}",
      inboundJson));

    IsInitialized = true;
    isLoaded = true;
    blocker.SetActive(false);

#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("OnEnter");
#endif
  }

  private void ApplyWeapons(MLFGGameData gameData)
  {
    weapons = gameData?.weapons;
    if (weapons != null && weapons.Count > 0)
      NormalWeaponId = weapons.ContainsKey("normal") ? "normal" : FirstKey(weapons);

    ApplyGunCost(0, NormalWeaponId);
    ApplyGunCost(1, MLFGWeaponIds.Torpedo);
    ApplyGunCost(2, MLFGWeaponIds.Laser);
  }

  private void ApplyGunCost(int index, string weaponId)
  {
    MLFGWeapon weapon = Weapon(weaponId);
    GunCosts[index] = weapon != null && weapon.cost > 0f ? weapon.cost : DefaultGunCosts[index];
  }

  // How many bounces this client simulates for a weapon's bullet before it counts as a miss.
  internal int BouncesFor(string weaponId) =>
    Weapon(weaponId)?.maxBounces ?? defaultMaxBounces;

  // Null = BulletView's serialized default speed.
  internal float? BulletSpeedFor(string weaponId)
  {
    float? speed = Weapon(weaponId)?.bulletSpeed;
    return speed > 0f ? speed : null;
  }

  // Seconds between shots. The server rejects a FIRE that arrives sooner than fireIntervalMs after
  // the last one, and network jitter can bunch FIREs sent exactly that far apart, so add a margin.
  private const float FireIntervalMargin = 1.1f;

  internal float FireIntervalFor(string weaponId, float fallback)
  {
    float? ms = Weapon(weaponId)?.fireIntervalMs;
    return (ms > 0f ? ms.Value / 1000f : fallback) * FireIntervalMargin;
  }

  private MLFGWeapon Weapon(string weaponId) =>
    weapons != null && !string.IsNullOrEmpty(weaponId) && weapons.TryGetValue(weaponId, out var w) ? w : null;

  private static string FirstKey(Dictionary<string, MLFGWeapon> map)
  {
    foreach (var key in map.Keys)
      return key;
    return "normal";
  }

  private void OnBalanceSync(JObject payload)
  {
    var sync = payload.ToObject<BalanceSyncPayload>();
    if (sync != null)
      UIManager.Instance.SyncBalance(sync.balance);
  }

  private void OnFishSpawned(JObject payload)
  {
    JToken fish = payload["fish"];
    if (fish is JArray list)
    {
      foreach (var entry in list)
        FishManager.Instance.SpawnFromServer(entry.ToObject<SpawnedFish>());
    }
    else if (fish is JObject single)
    {
      FishManager.Instance.SpawnFromServer(single.ToObject<SpawnedFish>());
    }
  }

  private void OnFishDespawned(JObject payload)
  {
    var despawn = payload.ToObject<FishDespawnedPayload>();
    if (despawn?.fishIds == null)
      return;

    foreach (var id in despawn.fishIds)
      FishManager.Instance.RemoveFish(id, despawn.reason);
  }

  private void OnShotResult(JObject payload)
  {
    var result = payload.ToObject<ShotResultPayload>();
    if (result == null)
      return;

    bool ownShot = IsOwnShot(result.shooterId);

    float payout = (float)(result.payout ?? 0);

    // Immortal and special fish are paid but keep swimming.
    if (result.outcome == MLFGShotOutcome.Reward || (result.outcome == MLFGShotOutcome.Kill && result.isDespawned == false))
    {
      if (ownShot)
        FishManager.Instance.PlayRewardPayout(result.fishId, payout, result.isCriticalHit == true);
      // TODO: show result.rewardLabel in a win banner (UI pending).
    }
    else if (result.outcome == MLFGShotOutcome.Kill)
    {
      if (ownShot)
        FishManager.Instance.PlayKillPayout(result.fishId, payout);
      FishManager.Instance.KillFish(result.fishId);
    }

    if (result.splashHits != null)
    {
      foreach (var hit in result.splashHits)
      {
        if (hit?.outcome == MLFGShotOutcome.Kill && hit.isDespawned != false)
          FishManager.Instance.KillFish(hit.fishId);
      }
    }

    if (!ownShot)
      GunManager.Instance?.HandleRemoteShot(result);

    if (ownShot && result.shooterBalance.HasValue)
      UIManager.Instance.ApplyServerBalance(result.shooterBalance.Value);
    else if (result.shooterBalance.HasValue)
      GameLog.Info(LogCategory.Balance, WithJson($"Ignored balance {result.shooterBalance.Value:N2} from shooter {result.shooterId} (we are {UserId})", inboundJson));
  }

  // Opponent bullets are visual only and never send SHOOT.
  private void OnBulletFired(JObject payload)
  {
    var fired = payload.ToObject<BulletFiredPayload>();
    if (fired == null || IsOwnShot(fired.shooterId))
      return;
    GunManager.Instance?.HandleRemoteFire(fired);
  }

  private void OnFormationAlert(JObject payload)
  {
    var alert = payload.ToObject<FormationAlertPayload>();
    GameLog.Info(LogCategory.Fish, WithJson("Formation alert: " + alert?.message, inboundJson));
    // TODO: tide warning banner (art pending).
  }

  // Our own join is also broadcast; game:init already set up our seat, so skip it.
  private void OnPlayerJoined(JObject payload)
  {
    var joined = payload.ToObject<PlayerSeatPayload>();
    if (joined == null || string.IsNullOrEmpty(joined.userId) || joined.userId == UserId)
      return;

    GameLog.Info(LogCategory.Socket, WithJson($"Player joined: {joined.username} ({joined.userId}) seat {joined.seatIndex}", inboundJson));
    GunManager.Instance?.OccupySeat(joined.userId, joined.username, joined.seatIndex);
  }

  private void OnPlayerLeft(JObject payload)
  {
    var left = payload.ToObject<PlayerSeatPayload>();
    if (left == null || string.IsNullOrEmpty(left.userId) || left.userId == UserId)
      return;

    GameLog.Info(LogCategory.Socket, WithJson($"Player left: {left.username} ({left.userId}) seat {left.seatIndex}", inboundJson));
    GunManager.Instance?.VacateSeat(left.userId);
  }

  // --------------------------------------------------------
  // OUTBOUND REQUESTS
  // --------------------------------------------------------

  // FIRE charges the shot. onAccepted runs on a successful ack; onRejected runs if the shot
  // can't be sent, times out or the server refuses it.
  internal void SendFire(string weaponType, string bulletId, Vector2 origin, float angle, int betIndex, Action onAccepted, Action onRejected)
  {
    var payload = new FirePayload
    {
      weaponType = weaponType,
      origin = new MLFGVec3(origin),
      angle = angle,
      clientTs = (long)ServerClock.NowMs,
      roomId = RoomId,
      bulletId = bulletId,
      betIndex = betIndex
    };

    bool sent = SendRequest(MLFGRequestTypes.Fire, payload, ack =>
    {
      if (ack == null)
      {
        onRejected?.Invoke();
        return;
      }

      if (IsRejected(ack))
      {
        string message = (string)(ack.SelectToken("payload.message") ?? ack["message"]);
        GameLog.Warn(LogCategory.Balance, WithJson($"FIRE {weaponType} {bulletId} rejected by server: {message}", ack.ToString(Formatting.None)));
        onRejected?.Invoke();
        UIManager.Instance.OnFireRejected(message);
        return;
      }

      ApplyAckBalance(MLFGRequestTypes.Fire, bulletId, ack);
      onAccepted?.Invoke();
    });

    if (!sent)
      onRejected?.Invoke();
  }

  // Every fired bullet sends exactly one SHOOT (hit or miss). It settles the shot and pays any
  // win; the bet was already charged by FIRE.
  internal void SendShoot(string weaponType, string bulletId, Vector2 origin, Vector2 lastBouncePoint, Vector2 aimTarget, int bounceCount, int betIndex, string targetFishId, double clientTs)
  {
    var payload = new ShootPayload
    {
      weaponType = weaponType,
      origin = new MLFGVec3(origin),
      lastBouncePoint = new MLFGVec3(lastBouncePoint),
      aimTarget = new MLFGVec3(new Vector2(Mathf.Round(aimTarget.x), Mathf.Round(aimTarget.y))),
      clientTs = (long)clientTs,
      roomId = RoomId,
      bulletId = bulletId,
      bounceCount = bounceCount,
      betIndex = betIndex,
      targetFishId = targetFishId
    };

    SendRequest(MLFGRequestTypes.Shoot, payload, ack =>
    {
      if (ack == null)
        return;

      if (IsRejected(ack))
      {
        GameLog.Warn(LogCategory.Balance, WithJson($"SHOOT {weaponType} {bulletId} rejected by server", ack.ToString(Formatting.None)));
        return;
      }

      ApplyAckBalance(MLFGRequestTypes.Shoot, bulletId, ack);
    });
  }

  private static bool IsRejected(JObject ack) =>
    ack["success"] is JValue success && success.Type == JTokenType.Boolean && !(bool)success;

  private void ApplyAckBalance(string type, string bulletId, JObject ack)
  {
    double? balance = ReadNumber(ack.SelectToken("payload.balance") ?? ack["balance"]);
    if (balance.HasValue)
      UIManager.Instance.ApplyServerBalance(balance.Value);
    else if (type == MLFGRequestTypes.Shoot)
      GameLog.Info(LogCategory.Balance, WithJson($"SHOOT {bulletId} ack has no balance (nothing paid)", ack.ToString(Formatting.None))); // normal on a miss
    else
      GameLog.Warn(LogCategory.Balance, WithJson($"{type} {bulletId} ack has no balance", ack.ToString(Formatting.None)));
  }

  // Every request carries an ack callback: the server always calls it.
  private bool SendRequest<T>(string type, T payload, Action<JObject> onAck)
  {
    string json = JsonConvert.SerializeObject(new MLFGRequest<T> { type = type, payload = payload });
    if (!IsConnected)
    {
      GameLog.Warn(LogCategory.Socket, WithJson($"Not connected, dropping {type}", json));
      return false;
    }

    if (GameLog.IsOn(LogCategory.Payload))
      GameLog.Info(LogCategory.Payload, WithJson($"⬆ {type}", json));

    TrackRequest(type);
    bool acked = false;
    MainGameSocket
      .ExpectAcknowledgement<JToken>(token =>
      {
        acked = true;
        requestLimitStrikes = 0; // the server is answering again
        if (GameLog.IsOn(LogCategory.Payload))
          GameLog.Info(LogCategory.Payload, WithJson($"⬇ ack {type}", RawJson(token)));
        onAck(ToAck(token));
      })
      .Timeout(TimeSpan.FromSeconds(requestAckTimeout), _ =>
      {
        if (acked) return;
        GameLog.Warn(LogCategory.Socket, WithJson($"{type} ack timed out", json));
        onAck(null);
      })
      .Emit(MLFGEvents.Request, json);
    return true;
  }

  private void TrackRequest(string type)
  {
    float now = Time.unscaledTime;
    recentRequests.Enqueue((now, type));
    while (recentRequests.Count > 0 && now - recentRequests.Peek().time > RequestHistorySeconds)
      recentRequests.Dequeue();
  }

  // e.g. "Sent 7 requests in the last 1 s, 21 in 5 s. Last: FIRE -0.62s, SHOOT -0.55s (+70ms), ..."
  private string RecentRequestSummary()
  {
    float now = Time.unscaledTime;
    int lastSecond = 0;
    foreach (var r in recentRequests)
    {
      if (now - r.time <= 1f)
        lastSecond++;
    }

    var parts = new List<string>();
    float? previous = null;
    int skip = Mathf.Max(0, recentRequests.Count - 8);
    foreach (var r in recentRequests)
    {
      if (skip-- > 0) { previous = r.time; continue; }
      string gap = previous.HasValue ? $" (+{(r.time - previous.Value) * 1000f:F0}ms)" : "";
      parts.Add($"{r.type} -{now - r.time:F2}s{gap}");
      previous = r.time;
    }

    return $"Sent {lastSecond} requests in the last 1 s, {recentRequests.Count} in {RequestHistorySeconds:F0} s. Last: {string.Join(", ", parts)}";
  }

  // Money may arrive as a number or a numeric string.
  private static double? ReadNumber(JToken token)
  {
    if (token == null)
      return null;
    if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
      return token.ToObject<double>();
    if (token.Type == JTokenType.String &&
        double.TryParse((string)token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value))
      return value;
    return null;
  }

  // Acks keep their envelope (balance may live under payload), so only parse strings here.
  private static JObject ToAck(JToken token)
  {
    if (token != null && token.Type == JTokenType.String)
    {
      try { token = JToken.Parse((string)token); }
      catch (JsonException) { return null; }
    }
    return token as JObject;
  }

  // Old-backend HIT request, still called by legacy fish code. Not supported by ML-FG.
  internal void SendHitEvent(string FishId, string WeaponType, string variant = "")
  {
    GameLog.Warn(LogCategory.Legacy, $"SendHitEvent ignored: '{WeaponType}' is not supported by the ML-FG backend yet");
  }

  // --------------------------------------------------------
  // FOCUS TIMEOUT
  // --------------------------------------------------------

  // Driven only by the JS visibility bridge (UIManager.OnFocusChanged), never OnApplicationFocus.
  internal void HandleFocusChange(bool focus)
  {
    hasFocus = focus;

    if (!focus)
    {
      focusLostTime = Time.time;
      if (focusCheckRoutine == null && !isExiting && !isBeingDestroyed)
        focusCheckRoutine = StartCoroutine(FocusTimeoutCheck());
    }
    else
    {
      if (focusCheckRoutine != null)
      {
        StopCoroutine(focusCheckRoutine);
        focusCheckRoutine = null;
      }
    }
  }

  private IEnumerator FocusTimeoutCheck()
  {
    while (!hasFocus && !isExiting && !isBeingDestroyed)
    {
      if (Time.time - focusLostTime >= maxBackgroundTime)
      {
        GameLog.Warn(LogCategory.Lifecycle, "Background timeout — closing connection");
        IsInitialized = false;
        ResetPingRoutine();

        if (MainSocketManager != null)
        {
          try { MainSocketManager.Close(); }
          catch (Exception e) { GameLog.Warn(LogCategory.Lifecycle, $"Focus close error: {e.Message}"); }
        }

        UIManager.Instance?.DisconnectionPopup();
        focusCheckRoutine = null;
        yield break;
      }

      yield return new WaitForSecondsRealtime(1f);
    }

    focusCheckRoutine = null;
  }

  // --------------------------------------------------------
  // EXIT
  // --------------------------------------------------------

  internal void CloseGame()
  {
    GameLog.Info(LogCategory.Lifecycle, "Closing game");
    StartCoroutine(CloseSocket());
  }

  internal IEnumerator CloseSocket()
  {
    isExiting = true;
    blocker.SetActive(true);
    ResetPingRoutine();

    if (IsConnected && !string.IsNullOrEmpty(RoomId))
      SendRequest(MLFGRequestTypes.Home, new HomePayload { roomId = RoomId }, _ => { });

    yield return new WaitForSecondsRealtime(0.2f);

    MainSocketManager?.Close();
    MainSocketManager = null;

    yield return new WaitForSecondsRealtime(0.5f);

    GameLog.Info(LogCategory.Lifecycle, "Socket closed");

#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("OnExit");
#endif
    Application.Quit();
  }
}

[Serializable]
public class AuthTokenData
{
  public string socketURL;
  public string cookie;
  public string nameSpace;
}
