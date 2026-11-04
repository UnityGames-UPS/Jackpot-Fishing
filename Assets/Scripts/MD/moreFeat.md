# ML-JF (Jackpot Fishing) - New Features Backend Specification & Network Contracts


## 1. Architectural Overview & Feature Matrix

The following table summarizes all newly added features, their server-authoritative control channels, and client-side expectations:

| Feature | Species / Action | Key Configuration | Server Contract | Client Lifecycle & Action |
| :--- | :--- | :--- | :--- | :--- |
| **Electric Cannon** | `CLAIM_ENERGY_BOOST` | `chargeThreshold: 100`, `referenceBet: 1.0` | Atomic CAS claim, turnover-weighted bet index validation | Process all caught targets in `kills` array and credit total win |
| **Immortal Ocean King** | `king_squid`, `golden_shark`, `golden_anglerfish` | `category: "immortal"`, `despawnOnKill: false` | Emits `outcome: "REWARD"`, keeps fish alive in Redis | Consecutive hit reward; **never destroy or remove game object on hit** |
| **Treasure Bowl** | `treasure_bowl` | `category: "immortal"`, `multipliers: [3, 50, 100, 168]` | Emits `isCriticalHit: true` when multiplier >= 50 | Consecutive hit reward; display Small Prize vs Critical Jackpot from `rewardLabel` |
| **Special Fish** | `armored_fish`, `crystal_anglerfish`, `fortune_terrapin` | `category: "immortal"`, `despawnOnKill: false` | Emits `isCriticalHit: true` only on 5x Super Win | Consecutive hit reward; base multiplier vs 5x Super Win distinguished via `isCriticalHit` |
| **Thunder Shark** | `thunder_shark` | `category: "effects"`, `chainRadius: 350`, `maxTargets: 5` | Emits `killEffect.type = "chain_lightning"` + `splashHits` | Primary kill despawns shark; secondary targets shocked in `splashHits` |
| **Torpedo Crab** | `torpedo_crab` | `category: "effects"`, `blastRadius: 200`, `torpedoCount: 3` | Emits `killEffect.type = "torpedo_blast"` + 3 blast areas | Primary kill despawns crab; fishes in 3 `blastAreas` resolved in `splashHits` |
| **Sea Anemone** | `sea_anemone` | `category: "effects"`, `pullRadius: 220`, `durationMs: 3500` | Emits `killEffect.type = "whirlpool"` + `splashHits` | Primary kill despawns anemone; fishes in `pullRadius` resolved in `splashHits` |

---

## 1.B Unified Client Data Contract

To keep client integration streamlined and avoid fragile multi-field checks, all spawned fish models and shot outcome payloads use a **minimal, unified contract**:

### 1. Fish Archetype on Spawn (`game:fish_spawned` / `activeFish`):
The client only needs to check **`fish.category`**:
- `"normal"` $\to$ Regular mortal fish. Dies when killed.
- `"immortal"` $\to$ Bosses (King Squid, Golden Shark, Golden Anglerfish, Treasure Bowl). Never despawns on hit (`isDespawned: false`).
- `"effects"` $\to$ Effects Fish (Thunder Shark, Torpedo Crab, Sea Anemone). Triggers post-kill screen weapon (`killEffect`).

### 2. Shot Outcome Payload (`game:shot_result` / `splashHits`):
The client only needs to evaluate **3 core fields**:
1. **`isDespawned` (`boolean`)**:
   - `true` $\to$ Fish was killed and removed from the room.
   - `false` $\to$ Fish remains alive in the room (continuous reward hit).
2. **`rewardLabel` (`string | undefined`)**:
   - The single authoritative display string resolved by the server (e.g. `"Small Prize (3x)"`, `"Critical Hit Jackpot"`, `"King Squid Rewards"`). If undefined, no banner is displayed.
3. **`isCriticalHit` (`boolean | undefined`)**:
   - `true` only when a critical jackpot payout is rolled.
4. **`killEffect` (`object | undefined`)**:
   - Populated only when an effects fish dies; contains type (`chain_lightning`, `torpedo_blast`, `whirlpool`) and coordinates/targets.

---

## 2. Full-Charge Energy Boost Electric Cannon

### A. Turnover Accumulation & Dynamic Bet Scaling

The Electric Cannon charges dynamically based on turnover from real paid shots:
$$\text{chargeAdded} = \text{shotCost} \times \left(\frac{\text{baseChargePerShot}}{\text{referenceBet}}\right)$$

1. **Charge Threshold:** When `chargeUnits >= chargeThreshold` ($100.0$), the energy state transitions to `isReady: true`.
2. **Turnover-Weighted Bet Firing:**
   - Players can choose which `betIndex` to fire their boost with.
   - Required turnover for firing at `betIndex`:
     $$\text{requiredTurnover} = \text{chargeThreshold} \times \left(\frac{\text{betAmount}}{\text{referenceBet}}\right)$$
   - If the player has accumulated $\$100$ turnover and fires at Bet $\$1.00$, the boost is valid.
   - If the player accumulated only $\$20$ turnover (by betting $\$0.20$ shots) and tries to claim at Bet $\$5.00$, the server **strictly rejects** the claim with `"Insufficient turnover accumulated for selected bet"`.
   - If the player accumulated $\$100$ turnover and fires at Bet $\$0.50$, the required $\$50$ turnover is consumed, and the remaining $\$50$ turnover **carries over** so player progress is never lost.

### B. Free Shot Rule & Balance Guarantee
The Electric Cannon is a **free promotional award**:
- The server **never deducts any balance** from the player when triggering `CLAIM_ENERGY_BOOST`.
- Only accumulated energy turnover is consumed.

### C. Claim Action Contract & Server CAS Settlement

#### Client Request: `CLAIM_ENERGY_BOOST`
```json
{
  "type": "CLAIM_ENERGY_BOOST",
  "payload": {
    "betIndex": 3,
    "requestId": "eb_req_1730299999000_uuid"
  }
}
```

#### Server ACK Response
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

### D. Broadcast Event: `game:energy_boost_claimed`
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

## 3. Immortal Ocean King System (Award Bosses)

### A. Continuous Reward Swimming Mechanic
Unlike regular mortal fishes that die and disappear when hit, **Immortal Ocean Kings** (`king_squid`, `golden_shark`, `golden_anglerfish`):
- Have `despawnOnKill: false` in their config.
- **Never despawn on hit.**
- Remain alive in the virtual coordinate space and continue swimming along their spline paths.
- Allow players to shoot them repeatedly for **consecutive prize payouts**.

### B. Multiplier Pools & Parsheet Dynamic Overrides
When hit, the server rolls a multiplier from the parsheet's `multipliers` array weighted by `multiplierWeights`:
```json
{
  "type": "king_squid",
  "name": "King Squid",
  "category": "immortal_boss",
  "despawnOnKill": false,
  "multipliers": [18, 36, 54, 72, 108],
  "multiplierWeights": [40, 30, 15, 10, 5],
  "rewardLabel": "King Squid Rewards"
}
```

### C. Server Shot Outcome: `REWARD` (`isDespawned: false`)

In `game:shot_result`:
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
> **Client Rule:**
> - Because `isDespawned == false` and `outcome == "REWARD"`, **DO NOT destroy or remove the fish game object**.
> - Keep the boss swimming along its path!

---

## 4. Treasure Bowl System (Dragon Turtle)

### A. Small Prize (3x) vs Critical Hit Jackpot (168x)
The **Treasure Bowl** (`treasure_bowl`) is a mystical golden dragon turtle carrying a treasure bowl:
- It is an immortal continuous reward fish (`despawnOnKill: false`).
- It pays two distinct reward categories governed by parsheet weights:
  1. **Small Prize (3x):** Standard consecutive hit prize.
  2. **Critical Hit Jackpot (50x, 100x, or 168x):** Rare jackpot hit.

```json
{
  "type": "treasure_bowl",
  "name": "Treasure Bowl",
  "category": "immortal_boss",
  "despawnOnKill": false,
  "multipliers": [3, 50, 100, 168],
  "multiplierWeights": [90, 6, 3, 1],
  "rewardLabel": "Small Prize (3x)",
  "criticalLabel": "Critical Hit Jackpot"
}
```

### B. Payload Fields: `isCriticalHit` & `criticalLabel`

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

## 5. Special Fish System (Consecutive 5x Super Win)

### A. Overview & 5x Super Win Mechanics
**Special Fish** (`armored_fish`, `crystal_anglerfish`, `fortune_terrapin`) are consecutive reward targets (`category: "immortal"`, `despawnOnKill: false`):
- **Rule:** *"When special fish species appear on the scene, players can consecutively gain the base multiplier rate by shooting and have a chance to win five times the Super Win."*
- Like Immortal Ocean Kings, they **never despawn on hit** (`isDespawned: false`).
- Hits roll a base multiplier with high probability (95%) and a $5\times$ Super Win multiplier with rare probability (5%).

### B. Parsheet Multipliers & Critical Thresholds

| Species | Type | Base Multiplier | Super Win Multiplier (5x) | `criticalThresholdMultiplier` |
| :--- | :--- | :---: | :---: | :---: |
| **Armored Fish** | `armored_fish` | `[20, 25, 30]` | `[100, 125, 150]` | `100` |
| **Crystal Anglerfish** | `crystal_anglerfish` | `[30, 35, 40]` | `[150, 175, 200]` | `150` |
| **Fortune Terrapin** | `fortune_terrapin` | `[40, 45, 50]` | `[200, 225, 250]` | `200` |

### C. Payload Contracts: Base Win vs 5x Super Win

#### Normal Consecutive Hit (Base Multiplier):
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

## 5.B Jackpot Fish (`jackpot_fish`) & Jackpot Dragon (`jackpot_dragon`)


| Fish Species | Species ID | Multiplier Pool | Critical / Mega Win Multiplier | Despawn On Kill |
| :--- | :--- | :--- | :--- | :--- |
| **Jackpot Fish** | `jackpot_fish` | `[66, 88, 168]` | `168` (`criticalThresholdMultiplier: 168`) | `true` (Mortal Boss) |
| **Jackpot Dragon** | `jackpot_dragon` | `[160, 240, 320]` | `320` (`criticalThresholdMultiplier: 320`) | `true` (Mortal Boss) |

### Payload Contracts:

#### 1. Jackpot Fish Kill (Base 66x / 88x):
```json
{
  "outcome": "KILL",
  "fishId": "jackpot_fish_001",
  "payout": 66.00,
  "multiplier": 66,
  "isDespawned": true,
  "rewardLabel": "Jackpot Fish Win"
}
```

#### 2. Jackpot Fish Mega Win (Top 168x):
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

#### 3. Jackpot Dragon Mega Win (Top 320x):
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

## 6. Effects Fish System

### A. Overview & Mortal Kill Rules
Unlike Immortal Ocean Kings, **Effects Fish ARE mortal**:
- Upon being killed, they die and explode.
- Their special ability triggers immediately **post-kill** (`onFishKilled`), catching secondary fishes across the screen.
- Server awards the sum of the primary kill payout + all secondary splash payouts in one consolidated `game:shot_result` broadcast.

### B. Thunder Shark (`thunder_shark`): Chain Lightning

#### Specification:
- **Game Rule:** *"After the kill, nearby fish will be shocked, and trigger Chain Lightning, the targeted fish will explode."*
- **Effect Type:** `"chain_lightning"`
- **Config Parameters:** `chainRadius: 350`, `maxTargets: 5`.
- **Server Math:** Calculates Euclidean distance from shark's death position to all active fish. Targets up to 5 closest fishes within $350\text{px}$.

#### Payload Example:
```json
{
  "outcome": "KILL",
  "fishId": "thunder_shark_881",
  "payout": 45.00, // 20x Shark + 25x Secondary Fishes
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

#### Specification:
- **Game Rule:** *"After the kill, throw Torpedoes in three different areas for a chance to catch the fish within the blast range."*
- **Effect Type:** `"torpedo_blast"`
- **Config Parameters:** `torpedoCount: 3`, `blastRadius: 200`.
- **Server Math:** Dynamically locks torpedoes onto actual living fishes currently swimming in the room (never empty water). The 3 blast centers are placed directly on/around targeted living fishes, and any other fish within `blastRadius` ($200\text{px}$) undergoes individual `hitChance` evaluation (Kill vs Miss).

#### Payload Example:
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

#### Specification:
- **Game Rule:** *"After the kill, create a Whirlpool in the same place for a chance to trap fish in the range."*
- **Effect Type:** `"whirlpool"`
- **Config Parameters:** `pullRadius: 220`, `durationMs: 3500`.
- **Server Math:** Creates a vortex zone at `origin` with radius $220\text{px}$. Fishes inside the radius are caught and collected in `splashHits`.

#### Payload Example:
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
> If an Immortal Fish (`king_squid`, `golden_shark`, `golden_anglerfish`, `treasure_bowl`) is caught inside a Chain Lightning arc, Torpedo blast, or Whirlpool trap:
> 1. The server **will award the payout** to the player.
> 2. The server **NEVER despawns the immortal fish**.
> 3. The `splashHits` item for that fish will explicitly have:
>    - `outcome: "REWARD"`
>    - `isDespawned: false`
> 4. **Client must NOT destroy or remove the fish!** The fish remains alive and active in the room.

---


