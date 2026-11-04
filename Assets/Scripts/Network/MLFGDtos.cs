using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

// Wire contract for the ML-FG (server-authoritative fish game) backend.
// Source of truth: the backend's reference client (Scripts/PHP/FishMock.php).
// All coordinates are server pixels: 1920x1080, origin top-left, y pointing down.

internal static class MLFGEvents
{
  // Client -> server
  internal const string Request = "request";
  internal const string TimeSync = "time_sync";

  // Server -> client
  internal const string RoomJoined = "room:joined";
  internal const string Init = "game:init";
  internal const string BalanceSync = "balance:sync";
  internal const string FishSpawned = "game:fish_spawned";
  internal const string FishDespawned = "game:fish_despawned";
  internal const string ShotResult = "game:shot_result";
  internal const string BulletFired = "game:bullet_fired";
  internal const string FormationAlert = "game:formation_alert";
  internal const string PlayerJoined = "game:player_joined";
  internal const string PlayerLeft = "game:player_left";
  // The server's "error" event is delivered by Best.SocketIO as SocketIOEventTypes.Error.
  internal const string TimeSyncAck = "time_sync_ack";
  internal const string Pong = "pong";
}

internal static class MLFGRequestTypes
{
  internal const string Fire = "FIRE";
  internal const string Shoot = "SHOOT";
  internal const string Home = "HOME";
}

// Keys of gameData.weapons, sent as weaponType. The normal gun's id comes from init
// (SocketIOManager.NormalWeaponId).
internal static class MLFGWeaponIds
{
  internal const string Normal = "normal";
  internal const string Torpedo = "torpedo";
  internal const string Laser = "laser";
}

internal static class MLFGShotOutcome
{
  internal const string MissGeometry = "MISS_GEOMETRY";
  internal const string MissGate = "MISS_GATE";
  internal const string Kill = "KILL";
  internal const string Reward = "REWARD"; // paid, but the fish stays alive
}

internal static class MLFGDespawnReason
{
  internal const string Killed = "killed";
  internal const string Expired = "expired";
}

[Serializable]
public class MLFGVec3
{
  public float x;
  public float y;
  public float z;

  public MLFGVec3() { }

  public MLFGVec3(Vector2 v)
  {
    x = v.x;
    y = v.y;
  }

  internal Vector2 ToVector2() => new Vector2(x, y);
}

[Serializable]
public class MotionKeyframe
{
  public double t; // ms since spawn
  public double d; // distance along the path, px
}

[Serializable]
public class SplinePathParams
{
  public List<MLFGVec3> points;
  public List<float> cumulativeLengths;
}

[Serializable]
public class SpawnedFish
{
  public string id;
  public string type;
  public string variant; // no longer in the server config; fish are matched by type
  public string category;
  public string pathType;
  public SplinePathParams pathParams;
  public float speed; // reference only, never used for movement
  public MLFGVec3 startPosition;
  public MLFGVec3 endPosition;
  public double spawnTime; // server ms; can be fractional
  public double lifespan;
  public float hitboxRadius;
  public float? multiplier; // absent on fish that roll from a multiplier pool
  public float? hitChance;
  public List<MotionKeyframe> motionKeyframes;
}

[Serializable]
public class MLFGWeapon
{
  public float cost;
  public float hitChanceMultiplier;
  // Client-side bullet simulation (the server doesn't simulate bullets). When absent,
  // gameData.maxBulletBounces / BulletView's serialized speed apply.
  public int? maxBounces;
  public float? bulletSpeed; // server px/s
  public float? fireIntervalMs;
}

[Serializable]
public class MLFGGameData
{
  public Dictionary<string, MLFGWeapon> weapons;
  public List<MLFGVec3> seats;
  public int maxBulletBounces = 3; // fallback until each weapon carries its own
  public int spawnInterval;
  public int maxConcurrentFish;
  public List<float> bets;
}

[Serializable]
public class MLFGPlayer
{
  public double balance;
  public string username;
  public string userId;
}

[Serializable]
public class MLFGInitData
{
  public MLFGGameData gameData;
  public List<SpawnedFish> activeFish;
  public int seatIndex = -1;
  public Dictionary<string, int> roomSeats;
  public MLFGPlayer player;
  public string roomId;
}

[Serializable]
public class RoomJoinedPayload
{
  public string roomId;
}

// game:player_joined / game:player_left. Also sent for our own join.
[Serializable]
public class PlayerSeatPayload
{
  public string userId;
  public string username;
  public int seatIndex = -1;
  public string roomId;
}

[Serializable]
public class FishDespawnedPayload
{
  public List<string> fishIds;
  public string reason;
}

[Serializable]
public class SplashHit
{
  public string fishId;
  public string outcome;
  public double? payout;
  public bool? isDespawned;
}

[Serializable]
public class ShotResultPayload
{
  public string shooterId;
  public string shooterUsername;
  public string shotId;
  public string weaponType;
  public double? amount; // the shot's charge
  public int? betIndex;
  public MLFGVec3 origin;
  public MLFGVec3 aimTarget;
  public string outcome;
  public string fishId;
  public double? payout;
  public double? multiplier;
  public bool? isDespawned;
  public string rewardLabel;
  public bool? isCriticalHit;
  public double? shooterBalance;
  public List<SplashHit> splashHits;
}

[Serializable]
public class BulletFiredPayload
{
  public string shooterId;
  public string weaponType;
  public MLFGVec3 origin;
  public float angle;
  public string bulletId;
  public int? betIndex;
  public double? shooterBalance;
  public string targetFishId; // torpedo and laser only
}

[Serializable]
public class FormationAlertPayload
{
  public string message;
}

[Serializable]
public class BalanceSyncPayload
{
  public double balance;
}

[Serializable]
public class TimeSyncAckPayload
{
  public long clientTime;
  public long serverTime;
}

[Serializable]
public class MLFGRequest<T>
{
  public string type;
  public T payload;
}

[Serializable]
public class FirePayload
{
  public string weaponType;
  public MLFGVec3 origin;
  public float angle;
  public long clientTs;
  public string roomId;
  public string bulletId;
  public int betIndex;
}

[Serializable]
public class ShootPayload
{
  public string weaponType;
  public MLFGVec3 origin;
  public MLFGVec3 lastBouncePoint;
  public MLFGVec3 aimTarget;
  public long clientTs;
  public string roomId;
  public string bulletId;
  public int bounceCount;
  public int betIndex;
  [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
  public string targetFishId; // hits only
}

[Serializable]
public class HomePayload
{
  public string roomId;
}
