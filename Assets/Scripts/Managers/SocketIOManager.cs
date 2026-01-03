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
  [SerializeField] private GameObject blocker;
  [SerializeField] private UIManager uiManager;
  private SocketManager MainSocketManager;
  private Socket MainGameSocket;
  [SerializeField] internal JSFunctCalls JSManager;
  [SerializeField] protected string TestSocketURI = "https://devrealtime.dingdinghouse.com/";
  protected string SocketURI = null;
  [SerializeField] private string testToken;
  protected string gameNamespace = "playground"; //BackendChanges
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
  private void Start()
  {
    // OpenSocket();
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
    Application.runInBackground = true;
    DOTween.Init();
    DOTween.defaultTimeScaleIndependent = true;
    blocker.SetActive(true);
    isLoaded = false;
  }

  private void OnApplicationFocus(bool hasFocus)
  {
    if (!hasFocus)
    {
      // App lost focus, start disconnect timer
      disconnectTimerCoroutine = StartCoroutine(DisconnectTimer());
    }
    else
    {
      // App regained focus, cancel disconnect timer
      if (disconnectTimerCoroutine != null)
      {
        StopCoroutine(disconnectTimerCoroutine);
        disconnectTimerCoroutine = null;
        Debug.Log("Disconnect timer cancelled. App regained focus.");
      }
    }
  }

  private IEnumerator DisconnectTimer()
  {
    Debug.Log($"App lost focus. Disconnect timer started for {disconnectDelay} seconds.");
    yield return new WaitForSeconds(disconnectDelay);

    Debug.Log("Disconnect timer finished. Disconnecting due to prolonged focus loss.");
    MainGameSocket.Disconnect();
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
      Debug.Log("Namespace used :" + gameNamespace);
      MainGameSocket = this.MainSocketManager.GetSocket("/" + gameNamespace);
    }
    // Set subscriptions
    MainGameSocket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
    MainGameSocket.On(SocketIOEventTypes.Disconnect, OnDisconnected); //Back2 Start
    MainGameSocket.On<Error>(SocketIOEventTypes.Error, OnError);
    MainGameSocket.On<string>("game:init", OnListenForEvent);
    MainGameSocket.On<string>("result", OnListenForEvent);
    MainGameSocket.On<string>("pong", OnPongReceived);

    MainSocketManager.Open();
  }

  private void OnListenForEvent(string obj)
  {
    Debug.Log("Event:" + obj);
    Root root = JsonConvert.DeserializeObject<Root>(obj);

    switch (root.id.ToLower())
    {
      case "initdata":
        ReqFishSpawn();
        blocker.SetActive(false);
        break;
      case "spawnresult":
        HandleSpawnResult(root);
        break;
    }
  }

  void ReqFishSpawn()
  {
    Debug.Log("Spawning Fish");
    RequestFishEvent obj = new();
    string json = JsonConvert.SerializeObject(obj);
    SendDataWithNamespace("request", json);
  }

  private void HandleSpawnResult(Root root)
  {
    if (root?.payload?.spawnBatches == null)
      return;

    if (spawnFlowRoutine != null)
      StopCoroutine(spawnFlowRoutine);

    spawnFlowRoutine = StartCoroutine(SpawnFlow(root.payload));
  }

  private IEnumerator SpawnFlow(Payload payload)
  {
    foreach (var batch in payload.spawnBatches)
    {
      yield return StartCoroutine(SpawnBatchSequential(batch));
    }

    // yield return new WaitForSeconds(payload.remainingTime / 1000f);

    ReqFishSpawn();
  }

  private IEnumerator SpawnBatchSequential(SpawnBatch batch)
  {
    if (batch.fishes == null || batch.fishes.Count == 0)
      yield break;

    // Convert batch spawnTime to reference
    long batchStartTime = Convert.ToInt64(batch.spawnTime);

    List<BaseFish> aliveFishes = new();

    foreach (var backendFish in batch.fishes)
    {
      long fishSpawnTime = Convert.ToInt64(backendFish.spawnTime);
      float delay = (fishSpawnTime - batchStartTime) / 1000f;

      if (delay > 0)
        yield return new WaitForSeconds(delay);

      FishData data = FishManager.Instance.ToFishData(backendFish);
      BaseFish fish = FishManager.Instance.SpawnFishFromBackend(data);

      if (fish != null)
        aliveFishes.Add(fish);
    }

    // 🧠 Wait until ALL fishes from this batch are despawned
    yield return new WaitUntil(() =>
      aliveFishes.TrueForAll(f => f == null || !f.gameObject.activeSelf)
    );
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
    SendPing();
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
        Debug.Log("JSON data sent: " + json);
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
  public double balance;
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
  public int multiplier;
  public object spawnTime;
  public int lifespan;
  public int screenWeightCost;
  public int hitPoints;
  public int maxHitPoints;
}

[Serializable]
public class Payload
{
  public List<SpawnBatch> spawnBatches;
  public int remainingTime;
}

[Serializable]
public class SpawnBatch
{
  public List<Fish> fishes;
  public object spawnTime;
  public string batchId;
}
