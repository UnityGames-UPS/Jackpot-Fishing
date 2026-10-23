using System;
using System.Collections.Generic;
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

internal static class MLFGShotOutcome
{
  internal const string MissGeometry = "MISS_GEOMETRY";
  internal const string MissGate = "MISS_GATE";
  internal const string Kill = "KILL";
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
  public string variant;
  public string pathType;
  public SplinePathParams pathParams;
  public float speed; // reference only, never used for movement
  public MLFGVec3 startPosition;
  public MLFGVec3 endPosition;
  public long spawnTime;
  public double lifespan;
  public float hitboxRadius;
  public float multiplier;
  public float hitChance;
  public List<MotionKeyframe> motionKeyframes;
}

[Serializable]
public class MLFGWeapon
{
  public float cost;
  public float hitChanceMultiplier;
}

[Serializable]
public class MLFGGameData
{
  public Dictionary<string, MLFGWeapon> weapons;
  public List<MLFGVec3> seats;
  public int maxBulletBounces = 3;
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
}

[Serializable]
public class ShotResultPayload
{
  public string shooterId;
  public string shooterUsername;
  public string shotId;
  public string weaponType;
  public MLFGVec3 origin;
  public MLFGVec3 aimTarget;
  public string outcome;
  public string fishId;
  public double payout;
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
}

[Serializable]
public class ShootPayload
{
  public string weaponType;
  public MLFGVec3 origin;
  public MLFGVec3 aimTarget;
  public long clientTs;
  public string roomId;
  public string bulletId;
  public int bounceCount;
}

[Serializable]
public class HomePayload
{
  public string roomId;
}
