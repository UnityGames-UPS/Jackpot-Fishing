# ML-JF (Jackpot Fishing) Unity Integration Guide

This guide provides the complete, authoritative specification for integrating the **ML-JF (Jackpot Fishing)** multiplayer fish game backend with a **Unity C# client** (using SocketIOUnity, BestHTTP, or Native WebSockets).

---

## Table of Contents
1. [Core Architectural Overview](#1-core-architectural-overview)
2. [Coordinate Space & Clock Synchronization](#2-coordinate-space--clock-synchronization)
3. [Event Inventory Summary](#3-event-inventory-summary)
4. [Step-by-Step Feature Integration](#4-step-by-step-feature-integration)
   - [A. Room Join & Mode Switching (`JOIN_LEVEL`)](#a-room-join--mode-switching-join_level)
   - [B. Gun Trigger Pull & Balance Deduction (`FIRE`)](#b-gun-trigger-pull--balance-deduction-fire)
   - [C. Opponent Bullets Broadcast (`game:bullet_fired`)](#c-opponent-bullets-broadcast-gamebullet_fired)
   - [D. Bounced Bullet Physics & `lastBouncePoint`](#d-bounced-bullet-physics--lastbouncepoint)
   - [E. Shot Resolution (`SHOOT` Hit vs Miss Expiry)](#e-shot-resolution-shoot-hit-vs-miss-expiry)
   - [F. Shot Result & Fish Despawn Broadcasts](#f-shot-result--fish-despawn-broadcasts)
   - [G. 30-Second Fish Freeze Ability (`FREEZE`)](#g-30-second-fish-freeze-ability-freeze)
   - [H. Fish Spawning & Spline Movement](#h-fish-spawning--spline-movement)
5. [Weapon Configs & Cooldown Rules](#5-weapon-configs--cooldown-rules)

---

## 1. Core Architectural Overview

### Two-Phase Server-Authoritative Billing
Unlike legacy systems that charged on hit, ML-JF implements **Two-Phase Authoritative Billing**:
1. **Phase 1 (`FIRE`):** Trigger pulled $\to$ server validates balance $\to$ atomically deducts shot cost $\to$ sets stage `"DEDUCTED"` $\to$ broadcasts bullet $\to$ returns deducted balance in ACK.
2. **Phase 2 (`SHOOT`):** Bullet hits fish or expires on wall $\to$ server verifies the `"DEDUCTED"` bullet $\to$ **does NOT deduct money again** $\to$ evaluates geometric raycast and kill roll $\to$ credits payout on kill $\to$ marks `"COMPLETED"`.

```
[Unity Client]                               [Node.js Game Engine]               [Redis / PostgreSQL]
      |                                                |                                   |
      |--- 1. FIRE (bulletId, amount) ---------------->|                                   |
      |                                                |-- Atomic Deduct Balance --------->| (Postgres)
      |                                                |-- Record stage="DEDUCTED" ------->| (Redis)
      |                                                |-- Broadcast game:bullet_fired     |
      |<-- 2. ACK { success: true, balance: 99.00 } ---|                                   |
      |    (Update Balance UI immediately!)            |                                   |
      |                                                |                                   |
      | ~~~ Bullet travels / bounces off walls ~~~     |                                   |
      |                                                |                                   |
      |--- 3. SHOOT (bulletId, lastBouncePoint, target)|                                   |
      |                                                |-- Verify stage=="DEDUCTED" ------>|
      |                                                |-- (No double charge!)             |
      |                                                |-- Raycast & RNG Kill Check        |
      |                                                |-- On KILL: Credit Payout -------->| (Postgres)
      |                                                |-- Broadcast game:shot_result      |
      |<-- 4. ACK { outcome: "KILL", payout: 50.0 } ---|                                   |
      |    (Spawn coin shower VFX & set balance)       |                                   |
```

---

## 2. Coordinate Space & Clock Synchronization

### Canvas Resolution
All 2D physics and fish spline positions operate on a standard virtual canvas:
- **Width:** `1920` units
- **Height:** `1080` units
- **Coordinate Origin:** Top-Left is `(0, 0)`; Bottom-Right is `(1920, 1080)`.
- **Cannon Origins (Seats):**
  - Seat 0: `(480, 1080)` (Bottom-Left)
  - Seat 1: `(1440, 1080)` (Bottom-Right)
  - Seat 2: `(480, 0)` (Top-Left)
  - Seat 3: `(1440, 0)` (Top-Right)

### Clock Offset Synchronization
All timestamps (`clientTs`) sent in actions must be in **Server Time** (milliseconds).
When connecting to Socket.IO:
```csharp
long clientSendMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
socket.Emit("time_sync", clientSendMs, (response) => {
    long clientRecvMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    long roundTrip = (clientRecvMs - clientSendMs) / 2;
    long serverTime = response.serverTime;
    clockOffsetMs = serverTime - (clientRecvMs - roundTrip);
});

// To get synchronized timestamp:
long currentServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + clockOffsetMs;
```

---

## 3. Event Inventory Summary

| Event Name | Direction | Socket Channel | Purpose |
| :--- | :--- | :--- | :--- |
| `game:join_level` / `JOIN_LEVEL` | Client $\to$ Server | `game:action` | Switch between Multiplayer (Joy Hall) and Single Player (Solo Hall). |
| `game:init` | Server $\to$ Client | Push / Join Ack | Initial room roster, active fish, active freeze, player balance. |
| `FIRE` | Client $\to$ Server | `game:action` | Trigger gun shot. **Deducts balance immediately**. |
| `game:bullet_fired` | Server $\to$ Client | Broadcast | Informs room that an opponent fired a bullet. |
| `SHOOT` | Client $\to$ Server | `game:action` | Resolves hit or wall expiry. **Credits winnings if killed**. |
| `game:shot_result` | Server $\to$ Client | Broadcast | Outcome of a shot (`KILL`, `MISS_GATE`, `MISS_GEOMETRY`) + payout. |
| `game:fish_spawned` | Server $\to$ Client | Broadcast | New fish entered screen with spline path and keyframes. |
| `game:fish_despawned` | Server $\to$ Client | Broadcast | Fish removed (either killed or expired). |
| `FREEZE` | Client $\to$ Server | `game:action` | Activates 30-second screen-wide fish freeze ($30$ coins). |
| `game:room_frozen` | Server $\to$ Client | Broadcast | Notifies room that fish motion is frozen. |
| `game:room_unfrozen` | Server $\to$ Client | Broadcast | Notifies room that freeze expired and unfreezes fish motion. |

---

## 4. Step-by-Step Feature Integration

### A. Room Join & Mode Switching (`JOIN_LEVEL`)

Unity players can select between:
- `"multiplayer"`: Standard 4-player Joy Hall room.
- `"single"`: Isolated 1-player private room.

#### Client Request
```json
{
  "type": "JOIN_LEVEL",
  "payload": {
    "level": "multiplayer" // or "single"
  }
}
```

#### Server ACK Response
```json
{
  "success": true,
  "payload": {
    "roomId": "room-joy-hall-98a21",
    "mode": "multiplayer",
    "noop": false
  }
}
```

#### Initialization Push (`game:init`)
Immediately after joining or reconnecting, the server emits `game:init`:
```json
{
  "gameData": {
    "bets": [0.1, 0.2, 0.5, 1.0, 2.0, 5.0, 10.0, 20.0, 50.0, 100.0],
    "weapons": {
      "normal": { "fireIntervalMs": 200, "bulletSpeed": 1200, "maxBounces": 3, "targetingMode": "raycast" },
      "laser": { "fireIntervalMs": 250, "bulletSpeed": 0, "maxBounces": 0, "targetingMode": "target_lock" },
      "torpedo": { "fireIntervalMs": 600, "bulletSpeed": 800, "maxBounces": 0, "targetingMode": "target_lock" }
    },
    "seats": [
      { "x": 480, "y": 1080, "z": 0 },
      { "x": 1440, "y": 1080, "z": 0 },
      { "x": 480, "y": 0, "z": 0 },
      { "x": 1440, "y": 0, "z": 0 }
    ],
    "maxBulletBounces": 3,
    "freeze": { "cost": 30.0, "durationMs": 30000 },
    "energyBoost": { "chargeThreshold": 100.0, "enabled": true }
  },
  "activeFish": [ /* array of SpawnedFish */ ],
  "activeFreeze": null, // or { startTime, durationMs: 30000, endTime } if frozen
  "seatIndex": 0,
  "roomSeats": { "usr_uuid_1": 0, "usr_uuid_2": 1 },
  "player": {
    "userId": "usr_uuid_1",
    "username": "Player1",
    "balance": 100.00
  }
}
```

---

### B. Gun Trigger Pull & Balance Deduction (`FIRE`)

Emit this the exact millisecond the player taps to shoot or auto-fires.

#### Client Request
- **Event:** `game:action` (or `FishEvents.CLIENT.REQUEST`)
```json
{
  "type": "FIRE",
  "payload": {
    "weaponType": "normal",
    "origin": { "x": 480, "y": 1080, "z": 0 },
    "angle": 45.2,
    "clientTs": 1730200000100,
    "roomId": "room-joy-hall-98a21",
    "bulletId": "bullet_1730200000100_a8f92z",
    "betIndex": 3 
  }
}
```

#### Server ACK Response (Success)
```json
{
  "success": true,
  "payload": {
    "bulletId": "bullet_1730200000100_a8f92z",
    "balance": 99.00
  }
}
```
> **Unity Action:** Update local UI balance to `payload.balance` (`99.00`) immediately.

#### Server ACK Response (Failure / Insufficient Balance)
```json
{
  "success": false,
  "payload": {
    "message": "Insufficient balance"
  }
}
```
> **Unity Action:** Destroy the local visual bullet immediately, play error sound/toast.

---

### C. Opponent Bullets Broadcast (`game:bullet_fired`)

#### Server Push
```json
{
  "shooterId": "usr_uuid_2",
  "weaponType": "normal",
  "origin": { "x": 1440, "y": 1080, "z": 0 },
  "angle": 135.0,
  "bulletId": "bullet_opp_8812a"
}
```
> **Unity Action:**
> - If `shooterId == localPlayerUserId`: **Ignore** (your bullet is already animating).
> - If `shooterId != localPlayerUserId`: Rotate opponent's cannon at seat index and spawn a visual bullet moving along `angle`.

---

### D. Bounced Bullet Physics & `lastBouncePoint`

To prevent straight-line collision bugs across walls, the server raycasts only from the **last bounce reflection point**:
1. Bullet spawns at `origin` ($480, 1080$).
2. Bullet hits right wall at $(1920, 420)$.
   - `lastBouncePoint` is set to $(1920, 420, 0)$.
   - `bounces = 1`.
3. When sending `SHOOT`, include `lastBouncePoint`. If the bullet never bounced, `lastBouncePoint` equals `origin`.

---

### E. Shot Resolution (`SHOOT` Hit vs Miss Expiry)

#### 1. Hit Resolution (`SHOOT-HIT`)
When the bullet's bounding circle intersects a living fish's hitbox radius:

```json
{
  "type": "SHOOT",
  "payload": {
    "weaponType": "normal",
    "origin": { "x": 480, "y": 1080, "z": 0 },
    "lastBouncePoint": { "x": 1920, "y": 420, "z": 0 },
    "aimTarget": { "x": 1240, "y": 380, "z": 0 },
    "clientTs": 1730200001650,
    "roomId": "room-joy-hall-98a21",
    "bulletId": "bullet_1730200000100_a8f92z",
    "betIndex": 3 ,
    "targetFishId": "fish_dragon_882"
  }
}
```

#### Server ACK Response on Kill
```json
{
  "success": true,
  "payload": {
    "shooterId": "usr_uuid_1",
    "shotId": "shot-d92fa1-uuid",
    "bulletId": "bullet_1730200000100_a8f92z",
    "outcome": "KILL",
    "fishId": "fish_dragon_882",
    "payout": 50.00,
    "balance": 149.00
  }
}
```

---

#### 2. Wall Expiry Resolution (`SHOOT-MISS`)
When the bullet bounces more than `maxBulletBounces` (3 times) without hitting any fish:

```json
{
  "type": "SHOOT",
  "payload": {
    "weaponType": "normal",
    "origin": { "x": 480, "y": 1080, "z": 0 },
    "lastBouncePoint": { "x": 300, "y": 1080, "z": 0 },
    "aimTarget": { "x": 250, "y": 1120, "z": 0 },
    "clientTs": 1730200002800,
    "roomId": "room-joy-hall-98a21",
    "bulletId": "bullet_1730200000100_a8f92z",
    "bounceCount": 4,
    "betIndex": 3
  }
}
```

#### Server ACK Response on Miss
```json
{
  "success": true,
  "payload": {
    "shooterId": "usr_uuid_1",
    "bulletId": "bullet_1730200000100_a8f92z",
    "outcome": "MISS_GEOMETRY",
    "payout": null,
    "balance": 99.00
  }
}
```

---

### F. Shot Result & Fish Despawn Broadcasts

Whenever any shot completes, the server broadcasts two events to all clients:

#### 1. `game:shot_result`
```json
{
  "shooterId": "usr_uuid_1",
  "shooterUsername": "Player1",
  "shotId": "shot-d92fa1-uuid",
  "bulletId": "bullet_1730200000100_a8f92z",
  "weaponType": "normal",
  "amount": 1.00,
  "betIndex": 3,
  "targetFishId": "fish_dragon_882",
  "origin": { "x": 480, "y": 1080, "z": 0 },
  "aimTarget": { "x": 1240, "y": 380, "z": 0 },
  "outcome": "KILL", // "KILL" | "MISS_GATE" | "MISS_GEOMETRY"
  "fishId": "fish_dragon_882",
  "payout": 50.00,
  "shooterBalance": 149.00,
  "energy": {
    "chargeUnits": 45.0,
    "chargeThreshold": 100.0,
    "isReady": false
  }
}
```
> **Unity Action:**
> - If `outcome == "KILL"`: Play coin shower at `aimTarget`, play death animation on `fishId`. If `shooterId == localUserId`, update your UI balance to `shooterBalance`.
> - If `outcome == "MISS_GATE"`: Fish was hit but survived $\to$ show bubble hit effect on fish.

#### 2. `game:fish_despawned`
```json
{
  "fishIds": ["fish_dragon_882"],
  "reason": "killed" // "killed" | "expired"
}
```
> **Unity Action:** Fade out and remove the game objects corresponding to `fishIds`.

---

### G. 30-Second Fish Freeze Ability (`FREEZE`)

Freezes all active fish in place for 30 seconds. Costs **30 coins**.

#### Client Request
```json
{
  "type": "FREEZE",
  "payload": {
    "requestId": "freeze_client_uuid_123"
  }
}
```

#### Server ACK Response
```json
{
  "success": true,
  "payload": {
    "balance": 70.00,
    "activeFreeze": {
      "startTime": 1730200005000,
      "durationMs": 30000,
      "endTime": 1730200035000,
      "activatorUserId": "usr_uuid_1"
    }
  }
}
```

#### Room Broadcast: `game:room_frozen`
```json
{
  "startTime": 1730200005000,
  "durationMs": 30000,
  "endTime": 1730200035000,
  "activatorUserId": "usr_uuid_1"
}
```

#### Room Broadcast: `game:room_unfrozen`
Triggered automatically when the 30-second timer expires:
```json
{
  "roomId": "room-joy-hall-98a21",
  "unfrozenAt": 1730200035000,
  "freezeDurationMs": 30000
}
```

#### Unity Spline Freeze Math:
While `activeFreeze != null` and `currentTime < activeFreeze.endTime`:
$$\text{effectiveSimulationTime} = \text{activeFreeze.startTime}$$
When `game:room_unfrozen` arrives:
For every active fish: `fish.spawnTime += activeFreeze.durationMs;`
This guarantees fish seamlessly resume moving from the exact pixel coordinate where they stopped without jumping or snapping!

---

### H. Fish Spawning & Spline Movement

#### Server Push: `game:fish_spawned`
```json
{
  "fish": [
    {
      "id": "fish_dragon_882",
      "type": "golden_dragon",
      "variant": "default",
      "pathType": "spline",
      "startPosition": { "x": 1920, "y": 200, "z": 0 },
      "endPosition": { "x": 0, "y": 800, "z": 0 },
      "pathParams": {
        "points": [
          { "x": 1920, "y": 200, "z": 0 },
          { "x": 1400, "y": 450, "z": 0 },
          { "x": 800, "y": 300, "z": 0 },
          { "x": 0, "y": 800, "z": 0 }
        ],
        "cumulativeLengths": [0, 574.89, 1205.12, 2120.45]
      },
      "speed": 120.0,
      "motionKeyframes": [
        { "t": 0, "d": 0 },
        { "t": 5000, "d": 600 },
        { "t": 12000, "d": 2120 }
      ],
      "spawnTime": 1730200000000,
      "lifespan": 15000,
      "hitboxRadius": 60,
      "multiplier": 50
    }
  ]
}
```

---

## 5. Weapon Configs & Cooldown Rules

| Weapon Type | Firing Speed | Behavior | Fire Interval | Shot Cost |
| :--- | :--- | :--- | :--- | :--- |
| **Normal Gun** (`normal`) | $1200\text{ px/s}$ | Bouncing bullet (up to 3 bounces) | $200\text{ms}$ ($5\text{ shots/sec}$) | Selected bet (e.g. $0.10$–$10.00$) |
| **Laser Cannon** (`laser`) | Instant ($0\text{ speed}$) | Direct target-lock ray (lightning) | $250\text{ms}$ | Selected bet $\times 5$ (or base $5.0$) |
| **Torpedo** (`torpedo`) | Fast missile | Direct target-lock splash | $600\text{ms}$ | Selected bet $\times 10$ (or base $10.0$) |

> [!IMPORTANT]
> **Client Cooldown Failsafe:** Unity client must disable canvas tapping during the weapon's `fireIntervalMs`. If Unity sends clicks faster than the interval, the server returns `{ success: false, message: "Fire rate limit exceeded" }`.
