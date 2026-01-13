using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Best.SocketIO;
using Best.SocketIO.Events;
using Newtonsoft.Json.Linq;
using System.Linq;
using DG.Tweening;
using Newtonsoft.Json;

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
  [SerializeField] private float SpawnEventInterval = 5f;
  [SerializeField] private float electricHitInterval = 1.5f;
  [SerializeField] internal List<int> bets = new();
  [SerializeField] internal List<float> GunCosts = new() { 1, 1, 6 }; //1: normal 2: torpedo 3: electric
  protected string gameNamespace = "playground";
  private bool hasEverConnected = false;
  private float lastPongTime = 0f;
  private float pingInterval = 2f;
  private bool waitingForPong = false;
  private int missedPongs = 0;
  private const int MaxMissedPongs = 5;
  private Coroutine PingRoutine;
  private string myAuth = null;
  internal bool isLoaded = false;
  internal bool PrevRoundAck = false;
  internal bool BetHistAck = false;
  internal bool ReceivedRecordAck = false;
  private Coroutine disconnectTimerCoroutine;
  private Coroutine spawnFlowRoutine;
  [SerializeField] private float disconnectDelay = 180f;
  internal float ElectricHitInterval => electricHitInterval;

  private void Start()
  {
    OpenSocket();
  }

  void ReceiveAuthToken(string jsonData)
  {
    Debug.Log("Received data: " + jsonData);
    // Do something with the authToken
    var data = JsonUtility.FromJson<AuthTokenData>(jsonData);
    SocketURI = data.socketURL;
    myAuth = data.cookie;
  }

  private void Awake()
  {
    Instance = this;
    Application.runInBackground = true;
    DOTween.Init();
    DOTween.defaultTimeScaleIndependent = true;
    // blocker.SetActive(true);
    isLoaded = false;
  }

  // private void OnApplicationFocus(bool hasFocus)
  // {
  //   if (!hasFocus)
  //   {
  //     disconnectTimerCoroutine = StartCoroutine(DisconnectTimer());
  //   }
  //   else
  //   {
  //     if (disconnectTimerCoroutine != null)
  //     {
  //       StopCoroutine(disconnectTimerCoroutine);
  //       disconnectTimerCoroutine = null;
  //       // Debug.Log("Disconnect timer cancelled. App regained focus.");
  //     }
  //   }
  // }

  private IEnumerator DisconnectTimer()
  {
    // Debug.Log($"App lost focus. Disconnect timer started for {disconnectDelay} seconds.");
    yield return new WaitForSeconds(disconnectDelay);

    Debug.Log("Disconnect timer finished. Disconnecting due to prolonged focus loss.");
    MainGameSocket?.Disconnect();
  }

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
    Func<SocketManager, Socket, object> authFunction = (manager, socket) =>
    {
      return new
      {
        token = testToken
      };
    };
    options.Auth = authFunction;
    // Proceed with connecting to the server
    SetupGameSocketManager(options);
#endif
  }

  private IEnumerator WaitForAuthToken(SocketOptions options)
  {
    // Wait until myAuth is not null
    while (myAuth == null)
    {
      Debug.Log("My Auth is null");
      yield return null;
    }
    while (SocketURI == null)
    {
      Debug.Log("My Socket is null");
      yield return null;
    }
    Debug.Log("My Auth is not null");
    // Once myAuth is set, configure the authFunction
    Func<SocketManager, Socket, object> authFunction = (manager, socket) =>
    {
      return new
      {
        token = myAuth
      };
    };
    options.Auth = authFunction;

    Debug.Log("Auth function configured with token: " + myAuth);

    // Proceed with connecting to the server
    SetupGameSocketManager(options);
  }

  private void SetupGameSocketManager(SocketOptions options)
  {
#if UNITY_EDITOR
    // Create and setup SocketManager for Testing
    this.MainSocketManager = new SocketManager(new Uri(TestSocketURI), options);
#else
    // Create and setup SocketManager
    this.MainSocketManager = new SocketManager(new Uri(SocketURI), options);
#endif

    if (string.IsNullOrEmpty(gameNamespace) | string.IsNullOrWhiteSpace(gameNamespace))
    {
      MainGameSocket = this.MainSocketManager.Socket;
    }
    else
    {
      // Debug.Log("Namespace used :" + gameNamespace);
      MainGameSocket = this.MainSocketManager.GetSocket("/" + gameNamespace);
    }
    // Set subscriptions
    MainGameSocket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
    MainGameSocket.On(SocketIOEventTypes.Disconnect, OnDisconnected); //Back2 Start
    MainGameSocket.On<Error>(SocketIOEventTypes.Error, OnError);
    MainGameSocket.On<string>("game:init", ParseResponse);
    MainGameSocket.On<string>("result", ParseResponse);
    MainGameSocket.On<string>("pong", OnPongReceived);

    MainSocketManager.Open();
  }

  private void ParseResponse(string obj)
  {
    // Debug.Log("RESP:" + obj);
    Root root = JsonConvert.DeserializeObject<Root>(obj);

    if (root.player != null && root.player.balance != 0)
    {
      UIManager.Instance?.UpdateBalance(root.player.balance);
    }

    switch (root.id.ToLower())
    {
      case "initdata":
        Debug.Log("INIT: " + obj);
        bets = root.gameData.bets;
        ApplyWeaponCosts(root.gameData);
        ApplyGameIntervals(root.gameData);
        SendFishSpawnEvent();
        UIManager.Instance.HandeGameInit();

#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("OnEnter");
#endif
        blocker.SetActive(false);
        SendPing();

        break;
      case "spawnresult":
        // Debug.Log("SPAWNRESULT: " + obj);
        HandleSpawnResult(root);
        break;
      case "hitresult":
        Debug.Log(obj);
        HandleHitResult(root);
        break;
    }
  }

  void SendFishSpawnEvent()
  {
    // Debug.Log("Sending Spawn Fish Req");
    RequestFishEvent obj = new();
    string json = JsonConvert.SerializeObject(obj);
    SendDataWithNamespace("request", json);
  }

  private void ApplyWeaponCosts(GameData gameData)
  {
    if (gameData?.weapons == null)
      return;

    float normalCost = gameData.weapons.normal?.cost ?? (GunCosts.Count > 0 ? GunCosts[0] : 1f);
    float torpedoCost = gameData.weapons.torpedo?.cost ?? (GunCosts.Count > 1 ? GunCosts[1] : 1f);
    float electricCost = gameData.weapons.electric?.cost ?? (GunCosts.Count > 2 ? GunCosts[2] : 1f);

    GunCosts = new List<float> { normalCost, torpedoCost, electricCost };
  }

  private void ApplyGameIntervals(GameData gameData)
  {
    if (gameData == null)
      return;

    if (gameData.spawnInterval > 0)
      SpawnEventInterval = gameData.spawnInterval / 1000f;

    if (gameData.lazerInterval > 0)
      electricHitInterval = gameData.lazerInterval / 1000f;
  }

  private void HandleSpawnResult(Root root)
  {
    if (root?.payload?.fish == null)
      return;

    if (spawnFlowRoutine != null)
      StopCoroutine(spawnFlowRoutine);

    spawnFlowRoutine = StartCoroutine(SpawnFlow(root.payload));
  }

  private IEnumerator SpawnFlow(Payload payload)
  {
    StartCoroutine(SpawnFishes(payload.fish));

    yield return new WaitForSecondsRealtime(SpawnEventInterval);

    SendFishSpawnEvent();
  }

  private IEnumerator SpawnFishes(List<Fish> fishes)
  {
    if (fishes == null || fishes.Count <= 0)
    {
      Debug.LogError("No fishes found in payload");
      yield break;
    }

    List<FishData> fishesData = new();
    fishes.ForEach((t) =>
    {
      FishData fishData = FishManager.Instance.ToFishData(t);
      if (fishData == null) Debug.LogError("Fish data not found for: " + t.variant);
      fishesData.Add(fishData);
    });

    if (fishesData.Count <= 0)
    {
      Debug.LogError("no fish data found in list");
      yield break;
    }

    SpawnBatchContext context = new SpawnBatchContext
    {
      moveRightToLeft = UnityEngine.Random.value > 0.5f,
      usePathSet = UnityEngine.Random.value > 0.25f   // ← your new requirement
    };

    foreach (FishData data in fishesData)
    {
      BaseFish fish = FishManager.Instance.SpawnFishFromBackend(data, context);
    }
  }

  internal void SendHitEvent(string FishId, string WeaponType, string variant = "")
  {
    HitEvent obj = new()
    {
      payload = new()
      {
        betIndex = UIManager.Instance.BetCounter,
        fishId = FishId,
        weaponType = WeaponType
      }
    };
    string json = JsonConvert.SerializeObject(obj);
    Debug.Log(json + variant ?? "");
    // Debug.Log("HIT: " + variant + " " + FishId + " with " + WeaponType);
    SendDataWithNamespace("request", json);
  }

  void HandleHitResult(Root root)
  {
    Payload hitResult = root.payload;

    if (GunManager.Instance.currentGun is LazerGun lazerGun)
    {
      lazerGun.OnHitResult();
    }

    if (hitResult == null)
    {
      Debug.LogError("Null hit result");
      return;
    }

    if (hitResult.isExpired && !string.IsNullOrEmpty(hitResult.fishId))
    {
      Debug.Log("Expired fish cleanup: " + hitResult.fishId);
      BaseFish expiredFish = FishManager.Instance
        .GetActiveFishes()
        .FirstOrDefault(x => x.data.fishId == hitResult.fishId);

      if (expiredFish != null)
        expiredFish.Die();
    }

    // Debug.Log("HIT RESULT:" + JsonConvert.SerializeObject(HitResult));
    if (!root.success)
    {
      if (!string.IsNullOrEmpty(hitResult.message) &&
          hitResult.message.IndexOf("amount refunded", StringComparison.OrdinalIgnoreCase) >= 0)
      {
        UIManager.Instance?.PlayRefundText(hitResult.totalBet);
      }
      return;
    }

    if (hitResult.fishKilled != null)
    {
      Debug.Log("Fish Killed: " + hitResult.fishKilled.variant);
      BaseFish fish = FishManager.Instance
        .GetActiveFishes()
        .FirstOrDefault(x => x.data.fishId == hitResult.fishKilled.id);

      if (fish == null)
      {
        Debug.LogError("No alive fish found to kill. " + hitResult.fishKilled.variant + " " + hitResult.fishKilled.id);
        return;
      }

      // if(HitResult.winAmount > UIManager.Instance.currentBet * UIManager.Instance.GetGunCost())
      fish.OnFishDespawned = () => UIManager.Instance?.PlayCoinBlastForFish(fish);

      TorpedoGun torpedoGun = null;
      if (GunManager.Instance?.Guns != null)
        torpedoGun = GunManager.Instance.Guns.OfType<TorpedoGun>().FirstOrDefault();

      bool isLocalTorpedoKill = false;
      if (torpedoGun != null && hitResult.weaponType == "torpedo")
      {
        isLocalTorpedoKill = torpedoGun.GetLockedFish() == fish;
      }

      bool isVisibleForTorpedo = IsFishVisibleForTorpedo(fish);

      if (hitResult.weaponType == "torpedo")
        torpedoGun?.OnFishKilled(fish);
      fish.MarkPendingDeath();

      // Weapon-aware death handling
      if (hitResult.weaponType == "torpedo")
      {
        fish.deathCause = BaseFish.DeathCause.Torpedo;
        if (isLocalTorpedoKill && isVisibleForTorpedo)
        {
          fish.KillOnTorpedoArrival = true;
          // Wait for torpedo (with short fail-safe)
          fish.WaitForTorpedoKill(2f);
        }
        else
        {
          fish.Die();
        }
      }
      else
      {
        fish.deathCause =
          hitResult.weaponType == "electric"
            ? BaseFish.DeathCause.Laser
            : BaseFish.DeathCause.Bullet;

        // Instant despawn for non-torpedo weapons
        fish.Die();
      }
    }
  }

  private bool IsFishVisibleForTorpedo(BaseFish fish)
  {
    if (fish == null)
      return false;

    return fish.TorpedoTargetVisible;
  }

  // Connected event handler implementation
  void OnConnected(ConnectResponse resp)
  {
    Debug.Log("✅ Connected to server.");

    if (hasEverConnected)
    {
      // uiManager.CheckAndClosePopups();
    }

    hasEverConnected = true;
    waitingForPong = false;
    missedPongs = 0;
    lastPongTime = Time.time;
  }

  private void OnError(Error err)
  {
    Debug.LogError("Socket Error Message: " + err);
#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("error");
#endif
  }

  private void OnDisconnected()
  {
    Debug.LogWarning("⚠️ Disconnected from server.");
    // uiManager.DisconnectionPopup();
    ResetPingRoutine();
  }

  private void OnPongReceived(string data)
  {
    // Debug.Log("✅ Received pong from server.");
    waitingForPong = false;
    missedPongs = 0;
    lastPongTime = Time.time;
    // Debug.Log($"⏱️ Updated last pong time: {lastPongTime}");
    // Debug.Log($"📦 Pong payload: {data}");
  }

  private void SendPing()
  {
    ResetPingRoutine();
    PingRoutine = StartCoroutine(PingCheck());
  }

  void ResetPingRoutine()
  {
    if (PingRoutine != null)
    {
      StopCoroutine(PingRoutine);
    }
    PingRoutine = null;
  }

  private IEnumerator PingCheck()
  {
    while (true)
    {
      // Debug.Log($"🟡 PingCheck | waitingForPong: {waitingForPong}, missedPongs: {missedPongs}, timeSinceLastPong: {Time.time - lastPongTime}");

      if (missedPongs == 0)
      {
        // uiManager.CheckAndClosePopups();
      }

      // If waiting for pong, and timeout passed
      if (waitingForPong)
      {
        if (missedPongs == 2)
        {
          // uiManager.ReconnectionPopup();
        }
        missedPongs++;
        Debug.LogWarning($"⚠️ Pong missed #{missedPongs}/{MaxMissedPongs}");

        if (missedPongs >= MaxMissedPongs)
        {
          Debug.LogError("❌ Unable to connect to server — 5 consecutive pongs missed.");
          // uiManager.DisconnectionPopup();
          yield break;
        }
      }

      // Send next ping
      waitingForPong = true;
      lastPongTime = Time.time;
      // Debug.Log("📤 Sending ping...");
      SendDataWithNamespace("ping");
      yield return new WaitForSeconds(pingInterval);
    }
  }

  private void SendDataWithNamespace(string eventName, string json = null)
  {
    // Send the message
    if (MainGameSocket != null && MainGameSocket.IsOpen)
    {
      if (json != null)
      {
        MainGameSocket.Emit(eventName, json);
        // Debug.Log("JSON data sent: " + json);
      }
      else
      {
        MainGameSocket.Emit(eventName);
      }
    }
    else
    {
      Debug.LogWarning("Socket is not connected.");
    }
  }

  internal void CloseGame()
  {
    Debug.Log("Unity: Closing Game");
    StartCoroutine(CloseSocket());
  }

  internal IEnumerator CloseSocket()
  {
    blocker.SetActive(true);
    ResetPingRoutine();

    Debug.Log("Closing Socket");

    MainSocketManager?.Close();
    MainSocketManager = null;

    Debug.Log("Waiting for socket to close");

    yield return new WaitForSeconds(0.5f);

    Debug.Log("Socket Closed");

#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("OnExit");
#endif
  }
}

[Serializable]
public class AuthTokenData
{
  public string socketURL;
  public string cookie;
  public string nameSpace;
}

[Serializable]
public class RequestFishEvent
{
  public string type = "SPAWN";
}

[Serializable]
public class GameData
{
  public List<int> bets;
  public int historyLimit;
  public JackpotValues jackpotValues;
  public long sessionStartTime;
  public Weapons weapons;
  public int spawnInterval;
  public int lazerInterval;
}

[Serializable]
public class Weapons
{
  public WeaponCost normal;
  public WeaponCost torpedo;
  public WeaponCost electric;
}

[Serializable]
public class WeaponCost
{
  public float cost;
  public int chargeRequired;
}

[Serializable]
public class JackpotValues
{
  public int mini;
  public int minor;
  public int major;
  public int grand;
}

[Serializable]
public class Player
{
  public float balance;
}

[Serializable]
public class Root
{
  public bool success;
  public string id;
  public GameData gameData;
  public Player player;
  public Payload payload;
}

[Serializable]
public class Fish
{
  public string id;
  public string type;
  public string variant;
  public int lifespan;
}

[Serializable]
public class Payload
{
  public List<Fish> fish;
  public int remainingTime;


  //HitResult
  public string message;
  public bool isExpired;
  public string weaponType;
  public float totalBet;
  public int winAmount;
  public object electricCharge;
  public Fish hitFish;
  public FishKilled fishKilled;
  public string fishId;
}

[Serializable]
public class FishKilled
{
  public string id;
  public string type;
  public string variant;
  public int multiplier;
  public long spawnTime;
  public int lifespan;
  public int screenWeightCost;
  public int hitPoints;
  public int maxHitPoints;
}

[Serializable]
public class HitPayload
{
  public int betIndex = 0;
  public string fishId;
  public string weaponType;
}


[Serializable]
public class HitEvent
{
  public string type = "HIT";
  public HitPayload payload;
}
