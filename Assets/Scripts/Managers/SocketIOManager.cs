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
  [SerializeField] private float electricHitInterval = 1.5f;
  [SerializeField] internal List<float> bets = new();
  [SerializeField] internal List<InitFishGroup> initFishes;
  [SerializeField] internal List<float> GunCosts = new() { 1, 1, 6 }; // 0: normal 1: torpedo 2: electric
  internal float ElectricHitInterval => electricHitInterval;

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
  internal int MaxBulletBounces { get; private set; } = 3;
  internal string NormalWeaponId { get; private set; } = "normal";
  internal bool IsInitialized { get; private set; }
  private List<MLFGVec3> seats;

  // Fish events wait here (in order) until the clock is synced, so spawns and their
  // despawns/kills are never applied out of order or against an unsynced clock.
  private readonly Queue<Action> waitingForClock = new();
  private float connectedAt;
  private string inboundJson; // raw JSON of the event being handled, for logs

  internal bool IsConnected => MainGameSocket != null && MainGameSocket.IsOpen;
  internal bool CanShoot => IsConnected && IsInitialized && ServerClock.IsSynced && !string.IsNullOrEmpty(RoomId);

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
      action();
    else
      waitingForClock.Enqueue(action);
  }

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
    GameLog.Error(LogCategory.Socket, "Socket error: " + err);
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
      GameLog.Info(LogCategory.Clock, $"Clock synced: offset {ServerClock.OffsetMs:F0} ms, rtt {ServerClock.LastRttMs:F0} ms");
    else if (GameLog.IsOn(LogCategory.Clock))
      GameLog.Info(LogCategory.Clock, $"time_sync rtt {ServerClock.LastRttMs:F0} ms, offset {ServerClock.OffsetMs:F0} ms");
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

  private void OnRoomJoined(JObject payload)
  {
    string roomId = payload.Value<string>("roomId");
    if (!string.IsNullOrEmpty(roomId))
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
    MaxBulletBounces = init.gameData?.maxBulletBounces ?? 3;
    ApplyWeapons(init.gameData);

    bets = init.gameData?.bets != null && init.gameData.bets.Count > 0
      ? init.gameData.bets
      : new List<float> { 1f };

    UIManager.Instance.ResetBalance(init.player?.balance ?? 0);
    UIManager.Instance.HandeGameInit();
    GunManager.Instance?.PlaceAtSeat(SeatOrigin);

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
      $"maxBounces {MaxBulletBounces}, balance {init.player?.balance}, activeFish {activeFish?.Count ?? 0}",
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
    var weapons = gameData?.weapons;
    if (weapons == null || weapons.Count == 0)
      return;

    NormalWeaponId = weapons.ContainsKey("normal") ? "normal" : FirstKey(weapons);
    if (weapons.TryGetValue(NormalWeaponId, out var normal) && normal != null && GunCosts.Count > 0)
      GunCosts[0] = normal.cost;
  }

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

    if (result.outcome == MLFGShotOutcome.Kill)
    {
      if (ownShot)
        FishManager.Instance.PlayKillPayout(result.fishId, (float)result.payout);
      FishManager.Instance.KillFish(result.fishId);
    }

    if (result.splashHits != null)
    {
      foreach (var hit in result.splashHits)
      {
        if (hit?.outcome == MLFGShotOutcome.Kill)
          FishManager.Instance.KillFish(hit.fishId);
      }
    }

    if (ownShot && result.shooterBalance.HasValue)
      UIManager.Instance.ApplyServerBalance(result.shooterBalance.Value);
    else if (result.shooterBalance.HasValue)
      GameLog.Info(LogCategory.Balance, $"Ignored balance {result.shooterBalance.Value:N2} from shooter {result.shooterId} (we are {UserId})");
  }

  // Opponent bullets are visual only and never send SHOOT. Rendering them is pending.
  private void OnBulletFired(JObject payload)
  {
    var fired = payload.ToObject<BulletFiredPayload>();
    if (fired == null || IsOwnShot(fired.shooterId))
      return;
    // TODO: spawn an opponent bullet at fired.origin / fired.angle once opponent cannons exist.
  }

  private void OnFormationAlert(JObject payload)
  {
    var alert = payload.ToObject<FormationAlertPayload>();
    GameLog.Info(LogCategory.Fish, "Formation alert: " + alert?.message);
    // TODO: tide warning banner (art pending).
  }

  // --------------------------------------------------------
  // OUTBOUND REQUESTS
  // --------------------------------------------------------

  internal void SendFire(string bulletId, Vector2 origin, float angle)
  {
    var payload = new FirePayload
    {
      weaponType = NormalWeaponId,
      origin = new MLFGVec3(origin),
      angle = angle,
      clientTs = (long)ServerClock.NowMs,
      roomId = RoomId,
      bulletId = bulletId
    };
    // The server invokes an ack for every request, so FIRE must carry one even though it's ignored.
    SendRequest(MLFGRequestTypes.Fire, payload, _ => { });
  }

  // Every fired bullet sends exactly one SHOOT (hit or miss); that is what charges the bet.
  internal void SendShoot(string bulletId, Vector2 origin, Vector2 aimTarget, int bounceCount, double clientTs)
  {
    var payload = new ShootPayload
    {
      weaponType = NormalWeaponId,
      origin = new MLFGVec3(origin),
      aimTarget = new MLFGVec3(new Vector2(Mathf.Round(aimTarget.x), Mathf.Round(aimTarget.y))),
      clientTs = (long)clientTs,
      roomId = RoomId,
      bulletId = bulletId,
      bounceCount = bounceCount
    };

    bool sent = SendRequest(MLFGRequestTypes.Shoot, payload, ack =>
    {
      if (ack == null)
      {
        UIManager.Instance.CancelShot(bulletId);
        return;
      }

      double? balance = ReadNumber(ack.SelectToken("payload.balance") ?? ack["balance"]);

      if (ack["success"] is JValue success && success.Type == JTokenType.Boolean && !(bool)success)
        GameLog.Warn(LogCategory.Balance, WithJson($"SHOOT {bulletId} rejected by server", ack.ToString(Formatting.None)));
      else if (!balance.HasValue)
        GameLog.Warn(LogCategory.Balance, WithJson($"SHOOT {bulletId} ack has no balance", ack.ToString(Formatting.None)));

      UIManager.Instance.ConfirmShot(bulletId, balance);
    });

    if (!sent)
      UIManager.Instance.CancelShot(bulletId);
  }

  // Every request carries an ack callback: the server always calls it.
  private bool SendRequest<T>(string type, T payload, Action<JObject> onAck)
  {
    if (!IsConnected)
    {
      GameLog.Warn(LogCategory.Socket, $"Not connected, dropping {type}");
      return false;
    }

    string json = JsonConvert.SerializeObject(new MLFGRequest<T> { type = type, payload = payload });
    if (GameLog.IsOn(LogCategory.Payload))
      GameLog.Info(LogCategory.Payload, WithJson($"⬆ {type}", json));

    bool acked = false;
    MainGameSocket
      .ExpectAcknowledgement<JToken>(token =>
      {
        acked = true;
        if (GameLog.IsOn(LogCategory.Payload))
          GameLog.Info(LogCategory.Payload, WithJson($"⬇ ack {type}", RawJson(token)));
        onAck(ToAck(token));
      })
      .Timeout(TimeSpan.FromSeconds(requestAckTimeout), _ =>
      {
        if (acked) return;
        GameLog.Warn(LogCategory.Socket, $"{type} ack timed out");
        onAck(null);
      })
      .Emit(MLFGEvents.Request, json);
    return true;
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

  // Paused weapons (torpedo, laser) still call the old HIT request. Not supported by ML-FG.
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
