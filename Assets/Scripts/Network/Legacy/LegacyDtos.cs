using System;
using System.Collections.Generic;

// PAUSED (old backend) DTOs. Still referenced by the legacy hit-result handler and the
// paytable (InitFishGroup / Multipliers).

[Serializable]
public class RequestFishEvent
{
  public string type = "SPAWN";
}

[Serializable]
public class GameData
{
  public List<float> bets;
  public int historyLimit;
  public JackpotValues jackpotValues;
  public long sessionStartTime;
  public Weapons weapons;
  public int spawnInterval;
  public int lazerInterval;
  public List<string> halls;
  public List<InitFishGroup> fishes;
}

[Serializable]
public class Weapons
{
  public WeaponCost normal;
  public WeaponCost torpedo;
  public WeaponCost lazer;
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
public class InitFishGroup
{
  public string type;
  public List<string> variants;
  public Multipliers multipliers;
}

[Serializable]
public class Multipliers
{
  public List<float> normal = new();
  public List<float> golden = new();
  public List<List<float>> special = new();
  public List<List<float>> specialWin = new();
  public List<List<float>> effect = new();
  public List<List<float>> immortal_ocean_king = new();
  public List<List<float>> jackpot_fish = new();
  public List<List<float>> jackpot_dragon = new();
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
  public float winAmount;
  public object electricCharge;
  public Fish hitFish;
  public FishKilled fishKilled;
  public string fishId;
  public EffectTriggered effectTriggered;
}

[Serializable]
public class EffectTriggered
{
  public string type;
  public List<AffectedFish> affectedFish;
  public List<float> bonusWins;
}

[Serializable]
public class AffectedFish
{
  public string id;
  public string type;
  public string variant;
  public int multiplier;
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
