# ML-FG (Fish Game) Unity Integration Guide

This document describes exactly how a Unity client integrates with the Fish Game (ML-FG) backend. The backend is completely server-authoritative. The client is responsible for rendering the game state, sending player actions (shots), and animating the results.

---

## Game Lifecycle & Core Mechanics

### 1. Server Authority

The server controls all game logic:

- **Fish Spawning & Pathing**: The server decides when fish spawn, their specific path (spline), speed, and lifespan.
- **Hit Detection**: When a player shoots, they send their aim coordinates. The server calculates if a fish was actually at those coordinates at that exact time (using lag compensation).
- **Payouts**: The server calculates RNG, determines if a hit is a KILL, calculates the payout, and deducts/credits balances.

### 2. Fish Movement (Splines)

Fish paths are completely deterministic. When a fish spawns, the server sends:

- A predefined `pathType` (e.g., `spline`).
- A list of `points` (Vec3) that form a Bezier/Catmull-Rom curve.
- A `speed` value and a `lifespan`.
- A `spawnTime` timestamp.

The Unity client **must** use these exact points and speed to move the fish locally. Because both the client and server use the same spline math, the fish's position on the client will match the server's expected position.

---

## Event Inventory

| Event Name             | Direction       | Trigger                                   | Unity Action                                |
| ---------------------- | --------------- | ----------------------------------------- | ------------------------------------------- |
| `game:action` (FIRE)   | Client → Server | Player clicks to shoot                    | Send immediately on click for visual sync   |
| `game:action` (SHOOT)  | Client → Server | Bullet hits fish OR expires after bounces | Send on hit or expiry — balance is deducted |
| `game:bullet_fired`    | Server → Client | Another player fired                      | Spawn opponent bullet, ignore if own userId |
| `game:fish_spawned`    | Server → Client | Server ticker spawns fish                 | Instantiate and animate fish along spline   |
| `game:fish_despawned`  | Server → Client | Fish leaves screen or dies                | Remove fish from screen immediately         |
| `game:shot_result`     | Server → Client | Server resolves a player's shot           | Show payout VFX, update balance if own shot |
| `game:formation_alert` | Server → Client | A "Fish Tide" is starting                 | Show screen warning text                    |

---

## Detailed Event Documentation

### 1. Initialization (On Room Join)

When the Unity client connects to a room via the Gateway, it receives an initialization payload (typically in the `join_room` ack or similar init event). For Fish Game, this is typed as `MLFGInitData`.

#### Payload Structure

```json
{
  "gameData": {
    "weapons": {
      "W1": { "cost": 1 },
      "W2": { "cost": 5 }
    },
    "fishes": [{ "type": "clownfish", "variants": ["default"] }],
    "spawnInterval": 1500,
    "maxConcurrentFish": 40,
    "seats": [
      { "x": 490, "y": 1080, "z": 0 },
      { "x": 1440, "y": 1080, "z": 0 }
    ],
    "maxBulletBounces": 3
  },
  "activeFish": [
    // Array of SpawnedFish objects (see game:fish_spawned)
  ],
  "seatIndex": 0,
  "roomSeats": {
    "user_uuid_here": 0
  },
  "player": {
    "balance": 1000.0,
    "username": "Player1",
    "userId": "your-own-uuid" // Store this — use to filter SHOT_RESULT and BULLET_FIRED events
  }
}
```

**Unity Action**:

1. Render weapons available based on `gameData.weapons`.
2. Map current players in the room to `roomSeats` and their visual cannons at `gameData.seats[seatIndex]`.
3. Read `gameData.maxBulletBounces`. When a bullet exceeds this bounce count without hitting a fish, **send a SHOOT event with `bounceCount > maxBulletBounces`** (see section 4). This ensures balance is always deducted.
4. Save `player.userId` — use it to determine which `SHOT_RESULT` and `BULLET_FIRED` events belong to this client.

---

### 2. `game:action` (Fire Bullet - Visual Only)

#### Direction: Client → Server

#### Purpose

Tells the server that a player has **just tapped/clicked to shoot a bullet**.
This is purely for visual purposes so the server can broadcast the bullet to other players (opponents) in real-time, allowing everyone to see the bullet fly and bounce. **No money is deducted, and no hits are processed here.**

#### When it happens

The exact millisecond the player taps/clicks to shoot (before the bullet even hits a fish).

#### Request JSON

```json
{
  "type": "FIRE",
  "payload": {
    "weaponType": "W1",
    "origin": { "x": 490, "y": 1080, "z": 0 },
    "angle": 45.5,
    "clientTs": 1712493021000,
    "bulletId": "bullet_abc123"
  }
}
```

- `weaponType`: ID of the weapon used.
- `origin`: Where the bullet was fired from.
- `angle`: The angle/trajectory of the shot in degrees.
- `clientTs`: Synchronized Server Timestamp (in milliseconds) of the exact moment the bullet was fired.
- `bulletId`: (Optional) Unique ID generated by the client to track this bullet's outcome.

**Unity Action**: Send this event immediately upon user click so opponents can see it.

---

### 3. `game:bullet_fired` (Opponent Fired)

#### Direction: Server → Client

#### Purpose

The server sends this to all clients (except the shooter) whenever someone fires a bullet, so you can render their bullet flying and bouncing in real-time.

#### Response JSON

```json
{
  "shooterId": "uuid-string",
  "weaponType": "W1",
  "origin": { "x": 1440, "y": 1080, "z": 0 },
  "angle": 45.5,
  "bulletId": "bullet_abc123"
}
```

**Unity Action**: When you receive this event, check `shooterId`. If it matches your own `player.userId` from init — **ignore it** (your local bullet is already visible). Otherwise, spawn a cyan/opponent bullet and let it move and bounce using Unity's physics engine.

> ⚠️ **CRITICAL:** Do NOT send any `SHOOT` events to the server for opponents' bullets. Only the original shooter's client sends hit/miss events!

---

### 4. `game:action` (Shoot)

#### Direction: Client → Server

#### Purpose

Tells the server the final outcome of a bullet — either it hit a fish, or it expired after too many bounces (a miss). The server **always deducts the weapon cost** when this event arrives, regardless of hit or miss.

#### When it happens

There are **two cases** when Unity must send this event:

1. **Hit**: The bullet physically collides with a fish on screen.
2. **Miss (Bullet Expired)**: The bullet has bounced off walls more than `gameData.maxBulletBounces` times without hitting anything. In this case, send SHOOT with the bullet's last position as `aimTarget` and the bounce count as `bounceCount`.

> ⚠️ **CRITICAL:** Always send SHOOT in both cases. If you only send on hit, the player gets **free bullets** for all missed shots.

#### Request JSON

```json
{
  "type": "SHOOT",
  "payload": {
    "weaponType": "W1",
    "origin": { "x": 490, "y": 1080, "z": 0 },
    "aimTarget": { "x": 2.5, "y": 3.1, "z": 0 },
    "clientTs": 1712493021000,
    "bounceCount": 1,
    "bulletId": "bullet_abc123"
  }
}
```

- `weaponType`: ID of the weapon used (must exist in init `gameData.weapons`).
- `origin`: Where the bullet was fired from.
- `aimTarget`: The exact world coordinates where the bullet hit a fish, **or the last position of the bullet if it expired**.
- `clientTs`: Synchronized Server Timestamp (in milliseconds) of the exact moment the bullet HIT the fish (or expired). Used by the server for lag compensation.
- `bounceCount`: How many times the bullet bounced off walls. Send even when the bullet expires — if `bounceCount > maxBulletBounces`, the server treats it as a confirmed miss.
- `bulletId`: (Optional) The same unique ID that was generated during the FIRE event to track this bullet.

#### ACK Response

The server always returns the player's new balance in the acknowledgement:

```json
{
  "success": true,
  "payload": {
    "balance": 95.5,
    "bulletId": "bullet_abc123"
  }
}
```

Use this to immediately update the player's balance display in the UI.

**Unity Action**: Do not wait for a response to animate the bullet. Fire the bullet locally immediately.

---

### 3. `game:fish_spawned`

#### Direction: Server → Client

#### Purpose

Notifies all clients in the room that new fish have entered the screen.

#### When it happens

Triggered by the server ticker (e.g. every 1500ms) or when a massive Formation (Fish Tide) begins.

#### Response JSON

```json
{
  "fish": [
    {
      "id": "72d9e33c-b943-46c1-b59b-5d59591192af",
      "type": "tortoise",
      "variant": "tortoise_a",
      "pathType": "spline",
      "startPosition": { "x": 1920, "y": 237, "z": 0 },
      "endPosition": { "x": 24, "y": 1080, "z": 0 },
      "pathParams": {
        "points": [
          { "x": 1920, "y": 237, "z": 0 },
          { "x": 1431.23, "y": 414.53, "z": 0 },
          { "x": 954.39, "y": 618.91, "z": 0 },
          { "x": 520.52, "y": 919.91, "z": 0 },
          { "x": 24, "y": 1080, "z": 0 }
        ],
        "cumulativeLengths": [0, 520.01, 1038.79, 1566.86, 2088.55]
      },
      "speed": 169.61,
      "motionKeyframes": [
        { "t": 0, "d": 0 },
        { "t": 3007, "d": 510 },
        { "t": 3662, "d": 520 },
        { "t": 6662, "d": 1029 },
        { "t": 7317, "d": 1039 },
        { "t": 10371, "d": 1557 },
        { "t": 11026, "d": 1567 },
        { "t": 14043, "d": 2079 },
        { "t": 14102, "d": 2089 }
      ],
      "spawnTime": 1787727459895,
      "lifespan": 14102,
      "hitboxRadius": 50,
      "multiplier": 25
    }
  ]
}
```

- `pathParams.points`: A list of 3D control points (Vec3) that form the path.
- `pathParams.cumulativeLengths`: An array of distances corresponding to each point. The final element is the total length of the path.
- `spawnTime`: Absolute server timestamp (in milliseconds) when the fish was created.
- `lifespan`: Total time the fish will be alive and moving (in milliseconds).
- `speed`: Movement speed. This is provided for reference but should generally NOT be used for direct movement logic to prevent multiplayer desync.
- `motionKeyframes`: (Optional) A pre-computed time-to-distance table (`t` = time in ms, `d` = distance in pixels). Used for variable speed fishes (e.g. slowing down or pausing mid-path).

### 🕒 Clock Synchronization (Crucial)

Because players can be in different timezones or have inaccurate local clocks, you **must never use the local device time (`DateTime.UtcNow`) directly for gameplay logic.** Doing so will cause severe desync (fish dying too early or freezing).

Instead, calculate a `ClockOffset` using the `time_sync` endpoint.

**Trigger Strategy:**

1. **Initial Sync:** When connecting to the socket, emit `time_sync` to calculate the initial latency and offset.
2. **Periodic Sync:** During gameplay, emit `time_sync` every 5-10 seconds in the background to continuously adjust for device clock drift (CPU throttling).

**The Calculation (Ping-Pong):**

1. Emit: `socket.Emit("time_sync", DateTime.UtcNow.Ticks);`
2. On `time_sync_ack`, extract `clientTime` and `serverTime`.
3. Calculate round-trip Latency: `Latency = (Current_Local_Time - clientTime) / 2`
4. Calculate Offset: `ClockOffset = serverTime - Current_Local_Time + Latency`
5. Get Exact Server Time: `Current_Server_Time = Current_Local_Time + ClockOffset`

**Shooting Bullets:**
When emitting the `FIRE` and `HIT` events, the `clientTs` in the payload must be the **`Current_Server_Time`** calculated using the offset, NOT your raw local time! This allows the server's 400ms Lag Compensation window to work flawlessly regardless of ping.

**How to Animate Fish Using Splines, Keyframes, & Cumulative Lengths:**

Do not manually calculate speed (`Distance = Speed * Time`) to move objects. Using speed for movement leads to desyncs when clients lag. Instead, use **Deterministic Time-Based Interpolation**:

1. **Calculate Elapsed Time:**
   `long elapsed = Current_Server_Time - spawnTime;`
2. **Calculate Progress (0.0 to 1.0):**
   - **If `motionKeyframes` exists (Variable Speed):** Find exactly what distance the fish should have covered by lerping between the keyframes using `GetDistanceFromKeyframes()`. Then divide by total distance.
   - **If NO `motionKeyframes` exists (Constant Speed):** Simply divide elapsed time by total lifespan `progress = elapsed / lifespan;`.
3. **Map Progress to Coordinates:**
   Pass the progress to `GetSplinePositionAt()` to get the exact (X,Y) coordinates on the curve.

**Unity C# Helper Implementation:**
Include these helpers in your Fish Controller to smoothly handle both constant and variable speeds:

```csharp
void Update() {
    long elapsed = GetCurrentServerTimeMs() - spawnTime;
    float progress = 0f;

    if (motionKeyframes != null && motionKeyframes.Count > 0) {
        float currentDist = GetDistanceFromKeyframes(elapsed, motionKeyframes);
        float totalDist = cumulativeLengths[cumulativeLengths.Length - 1];
        progress = totalDist > 0 ? currentDist / totalDist : 0f;
    } else {
        progress = (float)elapsed / lifespan;
    }

    // Move the fish!
    transform.position = GetSplinePositionAt(points, cumulativeLengths, progress);
}

private float GetDistanceFromKeyframes(long elapsedMs, List<Keyframe> keyframes) {
    if (keyframes.Count == 0) return 0f;
    Keyframe last = keyframes[keyframes.Count - 1];
    if (elapsedMs >= last.t) return last.d;

    for (int i = 0; i < keyframes.Count - 1; i++) {
        Keyframe a = keyframes[i];
        Keyframe b = keyframes[i + 1];
        if (elapsedMs >= a.t && elapsedMs < b.t) {
            float ratio = (float)(elapsedMs - a.t) / (b.t - a.t);
            return a.d + ratio * (b.d - a.d);
        }
    }
    return last.d;
}

private Vector3 GetSplinePositionAt(Vector3[] points, float[] cumulativeLengths, float t) {
    if (points.Length == 0) return Vector3.zero;
    if (points.Length == 1) return points[0];

    float totalLength = cumulativeLengths[cumulativeLengths.Length - 1];
    if (totalLength <= 0) return points[0];

    float targetDistance = t * totalLength;
    int low = 0, high = cumulativeLengths.Length - 1;
    while (low <= high) {
        int mid = (low + high) / 2;
        if (cumulativeLengths[mid] < targetDistance) low = mid + 1;
        else high = mid - 1;
    }

    int i = Mathf.Max(0, low - 1);
    int nextI = Mathf.Min(i + 1, points.Length - 1);
    if (i == nextI) return points[i];

    float dist1 = cumulativeLengths[i];
    float segmentLen = cumulativeLengths[nextI] - dist1;
    float localT = segmentLen > 0 ? (targetDistance - dist1) / segmentLen : 0;

    return Vector3.Lerp(points[i], points[nextI], localT);
}
```

**Important Edge Cases:**

- **Negative Time (Clock Lag):** If the server time is slightly behind and the `elapsed` is negative, hold the fish at Point 0.
- **Late Joiners:** If a player joins late, `elapsed` will already be large (e.g. 5000ms). The math automatically places the fish midway through the screen.
- **End of Path:** If the fish reaches its final point (`progress >= 1.0`), smoothly fade it out if the despawn event hasn't arrived yet.

---

### 4. `game:fish_despawned`

#### Direction: Server → Client

#### Purpose

Tells the client to remove fish from the screen.

#### When it happens

When a fish reaches the end of its lifespan (swims off-screen) OR when a player successfully kills it.

#### Response JSON

```json
{
  "fishIds": ["uuid-string-1", "uuid-string-2"],
  "reason": "killed" // or "expired"
}
```

---

### 5. `game:shot_result`

#### Direction: Server → Client

#### Purpose

Broadcasts the result of a shot to all players in the room.

#### When it happens

Shortly after a client sends `game:action` (SHOOT). The server processes the hit detection and broadcasts the outcome.

#### Response JSON

```json
{
  "shooterId": "user-uuid",
  "shooterUsername": "Player1",
  "shotId": "uuid-string",
  "weaponType": "W1",
  "origin": { "x": 0, "y": -5, "z": 0 },
  "aimTarget": { "x": 2.5, "y": 3.1, "z": 0 },
  "outcome": "KILL",
  "fishId": "fish-uuid",
  "payout": 50,
  "shooterBalance": 1250.5
}
```

- `outcome`: Can be `MISS_GEOMETRY` (shot nothing), `MISS_GATE` (hit a fish, but RNG decided it didn't die), or `KILL` (hit and killed).
- `payout`: Coins won if `outcome` is `KILL`.
- `shooterBalance`: The shooter's new authoritative balance.

**Unity Action**:

1. Update `shooterId`'s cannon rotation to match the shot if it was fired by another player.
2. **Check `shooterId === player.userId` (from init)**. Only update the balance UI if it's your own shot — otherwise another player's shot would update your displayed balance.
3. If `outcome === "KILL"`, play the coin explosion/payout animation on `fishId`'s current position, and update the shooter's UI balance to `shooterBalance`.
4. If `outcome === "MISS_GATE"`, play a small "hit but survived" impact effect (like a bubble pop) on the fish.

---

### 6. `game:formation_alert`

#### Direction: Server → Client

#### Purpose

Alerts players that a special event (Fish Tide / High Density Formation) is about to happen.

#### When it happens

Triggered automatically by the server when the room shifts into a "Tide" phase (e.g., every 5 minutes).

#### Response JSON

```json
{
  "message": "Fish Tide is Coming!"
}
```

**Unity Action**: Show a massive UI alert (like a siren or sliding text) across the screen. At this moment, all standard fish will despawn or swim away fast. Shortly after this event, the server will flood the client with `game:fish_spawned` events containing the special formation paths.
