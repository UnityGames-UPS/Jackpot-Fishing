# ML-JF (Jackpot Fishing)


## 1. Executive Summary & Network Flow

### A. Architectural Principles
1. **Server-Authoritative State:** All RNG rolls, hit verifications, payouts, jackpot triggers, and energy boost turnover tracking are calculated and committed authoritatively on the server via Redis.
2. **Minimal Unified Client Contract:** The Unity client determines render and animation behavior from clean flags (`fish.category`, `isDespawned`, `rewardLabel`, `isCriticalHit`, `killEffect`) without brittle client-side guessing.
3. **Multiplayer Synchronization:** All game room actions (bullet fires, kills, freeze, energy boosts, jackpot wins) are broadcast to all players in the room over the Socket.IO room channel.

### B. Client $\leftrightarrow$ Server Flow Summary
- **Client $\to$ Server:**
  - Standard shooting packets (`game:shoot` / `game:fire`). Jackpots trigger server-side automatically during normal bullet hits on eligible species.
  - Hall / Room switching packet (`JOIN_LEVEL`).
  - Electric Cannon claim packet (`CLAIM_ENERGY_BOOST`).
  - Screen freeze packet (`FREEZE`).
- **Server $\to$ Client Core Events:**
  - `game:init` — Emitted on room join or hall switch. Delivers starting jackpot pools, active hall config, allowed bets, active fish, seats, and player balance.
  - `game:jackpot_pools_update` — Throttled broadcast (~every 1 second) as player bets accrue into the progressive pools.
  - `game:jackpot_win` — Instant room broadcast whenever any player hits a Speed, Lucky, or JILI Grand jackpot.
  - `game:shot_result` — Broadcast containing hit/miss outcome, payout, balance update, `isDespawned`, `rewardLabel`, and optional `killEffect` / `splashHits`.
  - `game:energy_boost_claimed` — Broadcast when a player discharges their fully charged Electric Cannon.

---

## 2. The 3 Halls & Switching Specification

The game features 3 distinct halls with differing bet ranges and multiplayer capacities:

| Hall ID | Display Name | Mode | Max Players (`roomLimit`) | Bets Array | Starting & Min Bet (`bets[0]`) | Max Bet (`bets[last]`) |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`joy`** | Joy Hall | `multiplayer` | **3** | `[0.1, 0.2, 0.5, 1, 2, 5, 10]` | **`0.10`** | **`10.00`** |
| **`regal`** | Regal Hall | `multiplayer` | **3** | `[1, 2, 5, 10, 20, 50, 100]` | **`1.00`** | **`100.00`** |
| **`regal_single`** | Regal Hall (Single) | `single` | **1** | `[1, 2, 5, 10, 20, 50, 100]` | **`1.00`** | **`100.00`** |

### Client Room Switch Request (`JOIN_LEVEL`)
When the player selects a hall in the UI dropdown, emit:
```json
{
  "type": "JOIN_LEVEL",
  "payload": {
    "level": "regal"
  }
}
```
* **Accepted values for `level`:** `"joy"`, `"regal"`, `"regal_single"`.
* **Server Lifecycle:**
  1. Server atomically reserves a seat in the destination room under distributed lock.
  2. Server moves the socket into the destination room and teardowns the source seat.
  3. Server responds with `JOIN_LEVEL_ACK` containing fresh `gameData.bets` and `currentHall`, and automatically sends a fresh `game:init`.

---

## 3. Progressive Jackpot System (Speed, Lucky, JILI Grand)

The ML-JF engine maintains 3 server-authoritative progressive jackpot pools stored with atomic precision in Redis:
- **Speed Jackpot** (Seed: $\$50.00$)
- **Lucky Jackpot** (Seed: $\$200.00$)
- **JILI Grand Jackpot** (Seed: $\$1,000.00$)

### Event 1: `game:init`
* **When it arrives:** Sent immediately when a player connects, joins, or switches rooms.
* **Payload:**
```json
{
  "currentHall": "joy",
  "gameData": {
    "bets": [0.1, 0.2, 0.5, 1.0, 2.0, 5.0, 10.0]
  },
  "jackpotPools": {
    "speed": 50.45,
    "lucky": 201.20,
    "jili": 1004.80
  }
}
```
* **Fields:**
  * `currentHall`: Active hall (`"joy"` | `"regal"` | `"regal_single"`).
  * `gameData.bets`: Allowed bets array for the active hall. Cannon default bet is always `bets[0]`.
  * `jackpotPools.speed`: Current pool value for Speed Jackpot.
  * `jackpotPools.lucky`: Current pool value for Lucky Jackpot.
  * `jackpotPools.jili`: Current pool value for JILI Grand Jackpot.
* **Unity Action:** 
  1. Set the 3 top HUD jackpot counters to the starting values.
  2. Configure cannon bet selector buttons to `gameData.bets` and initialize cannon to `gameData.bets[0]`.

---

### Event 2: `game:jackpot_pools_update`
* **When it arrives:** Periodically throttled (~every 1 second) as shots in the room accrue turnover into the pools.
* **Payload:**
```json
{
  "speed": 51.10,
  "lucky": 202.15,
  "jili": 1006.40
}
```
* **Unity Action:** Smoothly interpolate (Lerp / number-roll) the HUD text display to the updated numbers.

---

### Event 3: `game:jackpot_win`
* **When it arrives:** Broadcast to all players in the room immediately when any player triggers a jackpot.
* **Payload:**
```json
{
  "tier": "speed",
  "tierName": "Speed Jackpot",
  "basePoolAmount": 50.54,
  "multiplierFactor": 2.0,
  "multiplierType": "multiplied",
  "totalJackpotWin": 101.08,
  "fishId": "dragon-10492",
  "fishType": "jackpot_dragon",
  "winnerUserId": "user-482",
  "winnerUsername": "PoseidonPro",
  "timestamp": 1730310245000
}
```
* **Fields:**
  * `tier`: `"speed"` | `"lucky"` | `"jili"`
  * `tierName`: `"Speed Jackpot"` | `"Lucky Jackpot"` | `"JILI Jackpot"`
  * `basePoolAmount`: Pool amount before multiplier was applied.
  * `multiplierFactor`: Multiplier number applied (`1.0`, `2.0`, or `5.0`).
  * `multiplierType`: `"normal"` | `"multiplied"` | `"re-multiplied"`
  * `totalJackpotWin`: Final payout credited to the winner (`basePoolAmount * multiplierFactor`).
  * `fishId`: ID of the target fish that awarded the jackpot.
  * `fishType`: Type of the fish (e.g. `"jackpot_dragon"`, `"king_squid"`).
  * `winnerUserId`: User ID of the winning player.
  * `winnerUsername`: Display name of the winning player.
  * `timestamp`: Epoch millisecond timestamp of the win.
* **Unity Action:**
  1. Show win celebration modal:
     - **Title:** `tierName` (e.g. `"SPEED JACKPOT"`)
     - **Badge / Subtitle:**
       - `multiplierType == "re-multiplied"` $\to$ `"5X RE-MULTIPLIED WIN!"`
       - `multiplierType == "multiplied"` $\to$ `"2X MULTIPLIED WIN!"`
       - `multiplierType == "normal"` $\to$ `"JACKPOT WIN!"`
     - **Prize Text:** `+$${totalJackpotWin}` (e.g. `+$101.08`)
     - **Footer:** `"Won by {winnerUsername} on {fishType}"`
  2. Trigger Jackpot Audio FX and coin fountain particle effects.
  3. Auto-dismiss popup after ~4 seconds.

---

### Bet Qualification Matrix (HUD Unlock & Multiplier Badges)
Display unlock / multiplier badges on the HUD depending on the player's currently selected bullet bet:

| Jackpot Tier | Minimum Bet to Unlock | Multiplied Bet (2x) | Re-Multiplied Bet (5x) |
| :--- | :--- | :--- | :--- |
| **Speed Jackpot** | Bet > `0.20` | Bet > `0.60` | — |
| **Lucky Jackpot** | Bet > `0.80` | Bet > `2.00` | — |
| **JILI Grand** | Bet > `5.00` | Bet > `10.00` | Bet > `80.00` |

### Eligible Fish Whitelist for Jackpots
Jackpots can only be won by hitting or killing these whitelist targets:
- `jackpot_dragon` (Jackpot Dragon — High trigger probability)
- `jackpot_fish` (Jackpot Fish — High trigger probability)
- `treasure_bowl` (Treasure Bowl)
- `king_squid` (Immortal King Squid)
- `golden_shark` (Immortal Golden Shark)
- `armored_fish` (Special Armored Fish)
- `anglerfish` / `golden_anglerfish` (Anglerfish)
- `fortune_terrapin` (Special Fortune Terrapin)

---

## 4. Full-Charge Energy Boost Electric Cannon

### A. Turnover Accumulation & Dynamic Bet Scaling
The Electric Cannon charges dynamically based on turnover from real paid shots:
$$\text{chargeAdded} = \text{shotCost} \times \left(\frac{\text{baseChargePerShot}}{\text{referenceBet}}\right)$$

1. **Charge Threshold:** When `chargeUnits >= chargeThreshold` ($100.0$), the energy state transitions to `isReady: true`.
2. **Turnover-Weighted Bet Firing:**
   - Players can choose which `betIndex` to fire their boost with.
   - Required turnover for firing at `betIndex`:
     $$\text{requiredTurnover} = \text{chargeThreshold} \times \left(\frac{\text{betAmount}}{\text{referenceBet}}\right)$$
   - If the player has accumulated $\$100$ turnover and fires at Bet $\$1.00$, the boost is valid.
   - If the player accumulated only $\$20$ turnover (from small $\$0.20$ shots) and attempts to claim at Bet $\$5.00$, the server **strictly rejects** the claim with `"Insufficient turnover accumulated for selected bet"`.
   - If the player accumulated $\$100$ turnover and fires at Bet $\$0.50$, the required $\$50$ turnover is consumed, and the remaining $\$50$ turnover **carries over** so player progress is never lost.

### B. Free Promotional Award Guarantee
The Electric Cannon is a **free promotional award**:
- The server **never deducts any balance** from the player when triggering `CLAIM_ENERGY_BOOST`.
- Only accumulated energy turnover is consumed.

### C. Client Request: `CLAIM_ENERGY_BOOST`
```json
{
  "type": "CLAIM_ENERGY_BOOST",
  "payload": {
    "betIndex": 3,
    "requestId": "eb_req_1730299999000_uuid"
  }
}
```

### D. Server ACK Response
```json
{
  "success": true,
  "payload": {
    "activatorUserId": "usr_alpha_123",
    "effectiveBet": 1.0,
    "kills": [
      {
        "fishId": "fish_sardine_01",
        "type": "sardine",
        "multiplier": 5,
        "payout": 5.0,
        "isDespawned": true
      },
      {
        "fishId": "squid_immortal_01",
        "type": "king_squid",
        "multiplier": 50,
        "payout": 50.0,
        "isDespawned": false
      }
    ],
    "totalWinAmount": 55.0,
    "energy": {
      "currentCharge": 0.0,
      "maxCharge": 100.0,
      "isReady": false,
      "accumulatedTurnover": 0.0,
      "shotsCount": 0
    }
  }
}
```

### E. Broadcast Event: `game:energy_boost_claimed`
The server broadcasts this event to all players in the room upon successful electric cannon discharge:
```json
{
  "activatorUserId": "usr_alpha_123",
  "activatorUsername": "PoseidonPro",
  "effectiveBet": 1.0,
  "kills": [ /* array of killed fish with coordinates and payouts */ ],
  "totalWinAmount": 55.0,
  "energy": { "currentCharge": 0, "maxCharge": 100, "isReady": false }
}
```

---

## 5. Unified Client Data Contract

To keep client integration streamlined and avoid fragile multi-field checks, all spawned fish models and shot outcome payloads use a minimal, clean contract:

### 1. Fish Archetype on Spawn (`game:fish_spawned` / `activeFish`):
The client only needs to check **`fish.category`**:
- `"normal"` $\to$ Regular mortal fish. Dies when killed.
- `"immortal"` $\to$ Bosses (King Squid, Golden Shark, Golden Anglerfish, Treasure Bowl) and Special Fish. Never despawns on hit (`isDespawned: false`).
- `"effects"` $\to$ Effects Fish (Thunder Shark, Torpedo Crab, Sea Anemone). Triggers post-kill screen weapon (`killEffect`).

### 2. Shot Outcome Payload (`game:shot_result` / `splashHits`):
The client only needs to evaluate **4 core fields**:
1. **`isDespawned` (`boolean`)**:
   - `true` $\to$ Fish was killed and removed from the room. Destroy or animate despawn.
   - `false` $\to$ Fish remains alive in the room (continuous reward hit). **Keep swimming!**
2. **`rewardLabel` (`string | undefined`)**:
   - The authoritative display string resolved by the server (e.g. `"Small Prize (3x)"`, `"Critical Hit Jackpot"`, `"King Squid Rewards"`). If undefined, display no special banner.
3. **`isCriticalHit` (`boolean | undefined`)**:
   - `true` only when a top-tier critical jackpot or 5x Super Win payout is rolled.
4. **`killEffect` (`object | undefined`)**:
   - Populated only when an effects fish dies; contains weapon type (`chain_lightning`, `torpedo_blast`, `whirlpool`) and coordinates/targets.

---

## 6. Immortal Ocean King System (Award Bosses)

### A. Continuous Reward Swimming Mechanic
Unlike regular mortal fishes that die and disappear when hit, **Immortal Ocean Kings** (`king_squid`, `golden_shark`, `golden_anglerfish`):
- Have `despawnOnKill: false` in their config.
- **Never despawn on hit.**
- Remain alive in the virtual coordinate space and continue swimming along their spline paths.
- Allow players to shoot them repeatedly for **consecutive prize payouts**.

### B. Server Shot Outcome: `REWARD` (`isDespawned: false`)
```json
{
  "shooterId": "usr_alpha_123",
  "outcome": "REWARD",
  "fishId": "squid_boss_7782",
  "payout": 36.00,
  "multiplier": 36,
  "isDespawned": false,
  "rewardLabel": "King Squid Rewards",
  "shooterBalance": 135.00
}
```

> [!IMPORTANT]
> **Client Rule:** Because `isDespawned == false` and `outcome == "REWARD"`, **DO NOT destroy or remove the fish game object**. Keep the boss swimming along its path!

---

## 7. Treasure Bowl System (Dragon Turtle)

### A. Small Prize (3x) vs Critical Hit Jackpot (168x)
The **Treasure Bowl** (`treasure_bowl`) is an immortal continuous reward fish (`despawnOnKill: false`) that pays two distinct reward tiers:
1. **Small Prize (3x):** Standard consecutive hit prize.
2. **Critical Hit Jackpot (50x, 100x, or 168x):** Rare critical jackpot hit.

### B. Payload Contracts:
#### Normal Hit (Small Prize):
```json
{
  "outcome": "REWARD",
  "fishId": "bowl_dragon_001",
  "payout": 3.00,
  "multiplier": 3,
  "isDespawned": false,
  "rewardLabel": "Small Prize (3x)",
  "isCriticalHit": false
}
```

#### Critical Hit Jackpot (168x):
```json
{
  "outcome": "REWARD",
  "fishId": "bowl_dragon_001",
  "payout": 168.00,
  "multiplier": 168,
  "isDespawned": false,
  "rewardLabel": "Critical Hit Jackpot",
  "isCriticalHit": true
}
```

---

## 8. Special Fish System (Consecutive 5x Super Win)

**Special Fish** (`armored_fish`, `crystal_anglerfish`, `fortune_terrapin`) are consecutive reward targets (`category: "immortal"`, `despawnOnKill: false`):
- Players gain consecutive base multiplier payouts by shooting them and have a chance to trigger a **$5\times$ Super Win**.
- They **never despawn on hit** (`isDespawned: false`).

| Species | Type | Base Multipliers | Super Win Multipliers (5x) | `criticalThresholdMultiplier` |
| :--- | :--- | :---: | :---: | :---: |
| **Armored Fish** | `armored_fish` | `[20, 25, 30]` | `[100, 125, 150]` | `100` |
| **Crystal Anglerfish** | `crystal_anglerfish` | `[30, 35, 40]` | `[150, 175, 200]` | `150` |
| **Fortune Terrapin** | `fortune_terrapin` | `[40, 45, 50]` | `[200, 225, 250]` | `200` |

### Payload Contracts:
#### Normal Consecutive Hit:
```json
{
  "outcome": "REWARD",
  "fishId": "armored_fish_001",
  "payout": 25.00,
  "multiplier": 25,
  "isDespawned": false,
  "rewardLabel": "Armored Fish Reward",
  "isCriticalHit": false
}
```

#### Rare 5x Super Win Hit:
```json
{
  "outcome": "REWARD",
  "fishId": "armored_fish_001",
  "payout": 125.00,
  "multiplier": 125,
  "isDespawned": false,
  "rewardLabel": "Super Win (5x)",
  "isCriticalHit": true
}
```

---

## 9. Legendary Jackpot Bosses (Jackpot Fish & Jackpot Dragon)

| Fish Species | Species ID | Multiplier Pool | Critical / Mega Win Multiplier | Despawn On Kill |
| :--- | :--- | :--- | :--- | :--- |
| **Jackpot Fish** | `jackpot_fish` | `[66, 88, 168]` | `168` (`criticalThresholdMultiplier: 168`) | `true` (Mortal Boss) |
| **Jackpot Dragon** | `jackpot_dragon` | `[160, 240, 320]` | `320` (`criticalThresholdMultiplier: 320`) | `true` (Mortal Boss) |

### Payload Contracts:
#### Jackpot Fish Mega Win (Top 168x):
```json
{
  "outcome": "KILL",
  "fishId": "jackpot_fish_001",
  "payout": 168.00,
  "multiplier": 168,
  "isDespawned": true,
  "isCriticalHit": true,
  "rewardLabel": "Jackpot Fish Mega Win"
}
```

#### Jackpot Dragon Mega Win (Top 320x):
```json
{
  "outcome": "KILL",
  "fishId": "jackpot_dragon_001",
  "payout": 320.00,
  "multiplier": 320,
  "isDespawned": true,
  "isCriticalHit": true,
  "rewardLabel": "Jackpot Dragon Mega Win"
}
```

---

## 10. Effects Fish System (Screen Weapons)

### A. Overview & Mortal Kill Rules
Unlike Immortal Ocean Kings, **Effects Fish ARE mortal**:
- Upon being killed, they die, explode, and despawn (`isDespawned: true`).
- Their special ability triggers immediately **post-kill** (`onFishKilled`), catching secondary fishes across the screen.
- Server awards the sum of the primary kill payout + all secondary splash payouts in one consolidated `game:shot_result` broadcast.

---

### B. Thunder Shark (`thunder_shark`): Chain Lightning
- **Game Rule:** *"After the kill, nearby fish will be shocked, and trigger Chain Lightning, the targeted fish will explode."*
- **Effect Type:** `"chain_lightning"`
- **Config Parameters:** `chainRadius: 350`, `maxTargets: 5`.
- **Payload Example:**
```json
{
  "outcome": "KILL",
  "fishId": "thunder_shark_881",
  "payout": 45.00,
  "multiplier": 20,
  "isDespawned": true,
  "killEffect": {
    "type": "chain_lightning",
    "origin": { "x": 620, "y": 480, "z": 0 },
    "targets": [
      { "fishId": "sardine_01", "pos": { "x": 680, "y": 510, "z": 0 } },
      { "fishId": "clownfish_02", "pos": { "x": 580, "y": 420, "z": 0 } }
    ]
  },
  "splashHits": [
    { "fishId": "sardine_01", "outcome": "KILL", "payout": 10.0, "isDespawned": true },
    { "fishId": "clownfish_02", "outcome": "KILL", "payout": 15.0, "isDespawned": true }
  ]
}
```

---

### C. Torpedo Crab (`torpedo_crab`): 3-Torpedo Screen Blast
- **Game Rule:** *"After the kill, throw Torpedoes in three different areas for a chance to catch the fish within the blast range."*
- **Effect Type:** `"torpedo_blast"`
- **Config Parameters:** `torpedoCount: 3`, `blastRadius: 200`.
- **Payload Example:**
```json
{
  "outcome": "KILL",
  "fishId": "torpedo_crab_332",
  "payout": 35.00,
  "multiplier": 15,
  "isDespawned": true,
  "killEffect": {
    "type": "torpedo_blast",
    "origin": { "x": 960, "y": 540, "z": 0 },
    "blastAreas": [
      { "x": 680, "y": 510, "radius": 200, "targetFishId": "sardine_01" },
      { "x": 730, "y": 470, "radius": 200, "targetFishId": "sardine_01" },
      { "x": 630, "y": 550, "radius": 200, "targetFishId": "sardine_01" }
    ]
  },
  "splashHits": [
    { "fishId": "sardine_01", "outcome": "KILL", "payout": 10.0, "isDespawned": true },
    { "fishId": "clownfish_02", "outcome": "MISS_GATE" }
  ]
}
```

---

### D. Sea Anemone (`sea_anemone`): Whirlpool Vortex Trap
- **Game Rule:** *"After the kill, create a Whirlpool in the same place for a chance to trap fish in the range."*
- **Effect Type:** `"whirlpool"`
- **Config Parameters:** `pullRadius: 220`, `durationMs: 3500`.
- **Payload Example:**
```json
{
  "outcome": "KILL",
  "fishId": "sea_anemone_114",
  "payout": 27.00,
  "multiplier": 12,
  "isDespawned": true,
  "killEffect": {
    "type": "whirlpool",
    "origin": { "x": 800, "y": 600, "z": 0 },
    "pullRadius": 220,
    "durationMs": 3500
  },
  "splashHits": [
    { "fishId": "trapped_sardine_01", "outcome": "KILL", "payout": 5.0, "isDespawned": true },
    { "fishId": "trapped_clown_02", "outcome": "KILL", "payout": 10.0, "isDespawned": true }
  ]
}
```

---

### E. Immortal Fish Safety in Effect Explosions

> [!CAUTION]
> **Strict Server Guarantee:**
> If an Immortal Fish (`king_squid`, `golden_shark`, `golden_anglerfish`, `treasure_bowl`, or Special Fish) is caught inside a Chain Lightning arc, Torpedo blast, or Whirlpool trap:
> 1. The server **will award the payout** to the player.
> 2. The server **NEVER despawns the immortal fish**.
> 3. The `splashHits` item for that fish will explicitly have:
>    - `outcome: "REWARD"`
>    - `isDespawned: false`
> 4. **Client must NOT destroy or remove the fish!** The fish remains alive and active in the room.

