# ML-JF Backend Port: Status

Tracks what the client does against the new ML-JF backend and what is left. **Update this file at the end of every session**: move finished items to Done, add new ones, and log the session at the bottom.

Config `id` is now `FISH-JF` (was `ML-FG` / `ML-JF`). The client never sends or reads it.

Sources, newest first. When they disagree, the config wins over the docs:
- `Assets/Scripts/JSONS/Config.json`: the server's live config (fish, weapons, halls, jackpots)
- `Assets/Scripts/MD/NewNewNewJFMD.md`: halls, progressive jackpots, electric cannon, unified fish contract
- `Assets/Scripts/MD/moreFeat.md`: immortal, special, jackpot and effect fish payloads
- `Assets/Scripts/MD/JackpotFishing_IntegrationGuide.md`: core protocol (FIRE/SHOOT, spawns, freeze, clock sync)

Status key: **Done**, **Partial**, **Todo**, **Blocked** (waiting on backend or art).

## Done

Wired in code but not yet tested against the live server.

| Feature | Notes |
|---|---|
| Connection, auth, `time_sync` clock and heartbeat | `SocketIOManager`, `ServerClock` |
| `game:init` | Balance, bets, seats, weapons and active fish |
| Server fish movement | `game:fish_spawned` spline + keyframes, `game:fish_despawned` |
| Fish lookup | `fishesData` is the source: each `FishData.variant` equals the server's `type`. Aliases and fallback removed |
| Normal gun `FIRE` | Sends `betIndex`; the ack balance is shown; a rejected FIRE removes the bullet |
| Normal gun `SHOOT` | Sends `lastBouncePoint`, `targetFishId` (hits only) and `betIndex`, on hit and on miss |
| Balance | Shown only from server values (init, FIRE/SHOOT ack, own `shot_result`, `balance:sync`); no local deduction |
| Weapon costs | Bet × `weapons.*.cost` (normal 1, torpedo 4, laser 1); used for the low-balance check and the torpedo button text |
| `game:shot_result` | `KILL` removes the fish; `REWARD` / `isDespawned: false` keeps it and plays coins (rainbow win on `isCriticalHit`); `splashHits` kills removed |
| Opponents | `game:bullet_fired` bullets, cannon rotation and seat balance |
| Torpedo `FIRE`/`SHOOT` | `weaponType: "torpedo"`. FIRE on launch, SHOOT on impact (`targetFishId` + the fish's server position at impact; a miss if the fish died or left mid-flight). SHOOT waits for the FIRE ack (`TorpedoShot`); a rejected FIRE removes the torpedo. Legacy `WaitForTorpedoKill` path unused |
| Laser `FIRE`/`SHOOT` | `weaponType: "laser"`. One FIRE + SHOOT per tick (SHOOT after the FIRE ack, stamped with the tick time), ≤ 3 FIREs awaiting ack. Beam, variant lock and retargeting unchanged |
| Target Lock / Torpedo buttons | Live again. Lock only → laser, torpedo (± lock) → torpedo, neither → normal |
| Fire rate | Every gun waits `weapons.*.fireIntervalMs` × 1.1 (the server enforces the interval on arrival, and jitter bunches FIREs sent exactly that far apart). Serialized rates are fallbacks only. The init log prints the intervals in use |
| Opponent bet / balance / buttons | Opponent rigs hide bet +/-; bet text from `betIndex` (`bullet_fired`, then `shot_result`), balance from `shooterBalance`. Rig starts at `bets[0]` |
| Fractional `spawnTime` | The server sends ms with decimals; `SpawnedFish.spawnTime` and `ServerPath.SpawnTime` are `double` |

## New `game:init` shape (`Assets/Scripts/JSONS/InitJson.json`)

| Field | Client status |
|---|---|
| `gameData.weapons.*` (`cost`, `fireIntervalMs`, `bulletSpeed`, `maxBounces`, `minBet`, `maxBet`) | Cost, interval, speed and bounces used. `GunCosts` falls back to 1 / 4 / 1 when `cost` is missing. `minBet` / `maxBet` unused |
| `gameData.bets` | Used; this is the current hall's list |
| `gameData.seats`, `seatIndex`, `roomSeats`, `player`, `activeFish` | Used |
| `gameData.fishes` (`type` + `variants`, equal names) | Not needed; lookup is by type |
| `gameData.levels`, `currentHall`, `halls` | Not parsed (see Halls) |
| `gameData.capabilities.freeze` | Not parsed (see Freeze) |
| `gameData.capabilities.energyBoost` (config + current `currentCharge`, `isReady`, `accumulatedTurnover`) | Not parsed (see Electric cannon) |
| `activeFreeze` | Not parsed (see Freeze) |
| `jackpotPools` | Not parsed (see Progressive jackpots) |
| `roomId` | Used. The live init sends it (format `<id>:room-<hall>-<uuid>`; missing from the sample file). `room:joined` is not sent on our own join, but did arrive when another player joined our room; it now logs its raw payload (Socket) to learn the shape. It still sets `RoomId` if it carries a `roomId` |

## To build

### Halls and rooms
How joining works now: the client sends no join request. On connect the server places the player in a room by itself (default hall `joy`) and pushes `game:init`, which carries `roomId` (`room:joined` is no longer sent). The reference mock does the same. There is no room list or lobby in the docs; `JOIN_LEVEL` only picks a hall, and the server picks the room inside it.
- **Todo** `JOIN_LEVEL` with `level`: `joy` / `regal` / `regal_single`. Handle `JOIN_LEVEL_ACK`, then the fresh `game:init`
- **Todo** Connect the existing Hall Selection panel (`UIManager.OnClickHallSelection`, `HallSelectionBtn`) to `JOIN_LEVEL`. It only animates open and closed today
- **Done** `BetCounter` resets to 0 on every `game:init` (`UIManager.OnBetChanged`)
- **Todo** Read `currentHall` from init and show the hall name
- **Done** Player joined / left: `game:player_joined` → `GunManager.OccupySeat` (shows the rig with the username), `game:player_left` → `VacateSeat` (hides it). Payload `{ userId, username, seatIndex, roomId }`; our own join is also broadcast and skipped. `room:joined` (`{ roomId, userId, playerCount }`) is broadcast on every join and only sets `RoomId` for our own `userId`. `playerCount` unused
- **Done** Opponent bet text: `bets[0]` when the rig appears, then each shot's `betIndex`. Bet +/- hidden on opponent rigs
- **Done** Opponent gun follows `bullet_fired.weaponType` (instant switch, no animation). Torpedo: visual-only torpedo at `targetFishId` (no `TorpedoShot`, so no SHOOT). Laser: beam on `targetFishId`, kept up for 2 intervals per tick. No live target → the gun turns to `angle` and flashes. Field name `targetFishId` assumed; not yet checked against a live payload
- **Todo** Opponents already in the room when we join show no name (init's `roomSeats` has only ids) and a balance of 0 until their first `shot_result`. Ask the backend for usernames/balances in init, or a `player_joined` replay Today an opponent appears only once they fire (`GunManager.ResolveOpponentRig` claims a seat from the bullet origin), and a leaver's cannon stays until our next `game:init`. When the events exist, call the ready hooks `GunManager.OccupySeat(userId, username, seatIndex)` and `VacateSeat(userId)`
- **Done** Seats: config has 3 bottom seats (x 480 / 960 / 1440). `GunManager.SetupSeats` places rigs from the server's seats and hides the extra 4th rig on every init, so the scene needs no change. Minor: `SeatRig.SeatIndex` is never reset, so it would only matter if the server's seat count shrank mid-session

### Progressive jackpots (Speed / Lucky / JILI)
- **Todo** Read `jackpotPools` from `game:init` into 3 HUD counters
- **Todo** `game:jackpot_pools_update`: roll the counters to the new values
- **Todo** `game:jackpot_win` modal: title `tierName`; badge by `multiplierType` (normal / 2X / 5X re-multiplied); `+totalJackpotWin`; "Won by {winnerUsername} on {fishType}"; audio + coin fountain; closes itself after ~4 s
- **Todo** Bet-qualification badges per tier from `jackpotConfig.tiers` (`minBet`, `multipliedBet`, `reMultipliedBet`)
- **Blocked** UI art for the HUD counters, modal and badges

### Electric cannon (energy boost)
- **Todo** Energy meter from `energy` in `game:shot_result` (`chargeUnits`, `chargeThreshold`, `isReady`)
- **Todo** `CLAIM_ENERGY_BOOST` (`betIndex`, `requestId`); handle the ack's `kills[]` (respect `isDespawned`), `totalWinAmount` and the reset `energy`
- **Todo** Handle the `Insufficient turnover accumulated for selected bet` rejection
- **Todo** `game:energy_boost_claimed` broadcast for opponents' discharges
- **Blocked** UI and VFX. The old `LazerGun` "electric" gun is now the laser weapon, not this feature

### Freeze
- **Todo** `FREEZE` request (`requestId`), ack balance and `activeFreeze`
- **Todo** `game:room_frozen` / `game:room_unfrozen`, plus `activeFreeze` in init
- **Todo** Freeze the server path clock: hold sim time at `startTime`, then shift each fish's `spawnTime` by `durationMs` on unfreeze (`ServerPath` / `BaseFish`)
- **Blocked** UI button and frozen overlay

### Fish behaviour
- **Todo** `rewardLabel` / `criticalLabel` win banner for immortal, special and jackpot fish (`TODO` in `SocketIOManager.OnShotResult`)
- **Todo** Jackpot fish and dragon mega win on `isCriticalHit` for `KILL`. Reconnect `JackpotDragon.TriggerDeathSequence` (currently legacy-only)
- **Todo** Effect fish `killEffect`. Drive the existing `EffectFish` routines from server data instead of local targeting:
  - `thunder_shark`: chain lightning routine (was blue fish), `killEffect.targets`
  - `torpedo_crab`: torpedo routine (was rock crab), `killEffect.blastAreas`
  - `sea_anemone`: whirlpool routine (was bubble crab), `killEffect.origin` / `pullRadius` / `durationMs`
- **Todo** Splash `REWARD` items (immortal fish caught in effects): pay out without killing
- **Todo** Treasure bowl bucket and immortal win animations from the old pass (see `LegacyHitResultHandler`)
- **Done** Curvy removed from scripts (`CurvyPathProvider`, `SpawnBatchContext`, spline/speed code in `BaseFish.Legacy`, `NormalFish`, `EffectFish`, `ImmortalFish`). Fish move only on server paths; king squid keeps only its bubble-spread loop
- **Todo** Rock crab escape (`EffectFish.StartRockCrabEscape`) was a Curvy speed burst; now it just fades out with `Leave()`. Replace when `torpedo_crab` `killEffect` lands
- **Todo** Opponents' kills and rewards show no coins or wins (only our own)
- **Later** Hitboxes are much smaller than the art (e.g. `jackpot_dragon` radius 90 on a 1920 px sprite). Backend to raise them

### Weapons
- **Todo** Bet-based art: changing the bet swaps the gun, bullet and crosshair graphics. Hook: `UIManager.OnBetChanged`
- **Todo** Backend to deploy the new `fireIntervalMs` from `Config.json`: normal 250, laser 300, torpedo 600. At 160 / 200 / 500 (+10%) the server still returned `Too many request requests` and some `Fire rate limit exceeded` with `remainingMs` 5–10 (arrival jitter)
- **Done** After a `Too many request requests` socket error, `CanShoot` is false on every gun for 1.5 s, doubling per repeat up to 12 s; resets on the next ack. The error log lists the recent requests and gaps
- **Todo** `IsValidTorpedoTarget` uses a hardcoded list of normal fish; consider using fish category instead

### Other
- **Todo** `game:formation_alert` banner (`formationPhaseConfig.textAnimationDurationMs` 3000); only logged now
- **Todo** Paytable multipliers (`UIManager.UpdatePaytableMultipliers` is paused). Fill from config `multiplier` / `multipliers` if init sends the fish list
- **Todo** Docs: update `CLAUDE.md` architecture sections once the legacy code is removed

## Open questions for the backend
1. Special fish: config says `category: "special"` with no `despawnOnKill`; docs say `immortal` and never despawn. Which one?
2. Anglerfish naming: config `anglerfish`; docs say `crystal_anglerfish` and `golden_anglerfish`.
3. Treasure bowl critical tiers: config `[3, 168, 666, 888]`; docs say 50 / 100 / 168.
4. `roomLimit`: halls say 3, top level says 4, seats list 3.
5. ~~Full `game:init` shape~~ Answered, see `InitJson.json`. ~~Why did `weapons.*.cost` disappear?~~ Back in config and init. ~~Is `room:joined` still sent?~~ No, but the live init carries `roomId`.
6. Requests: still `request` with `{ type, payload }`, or the new `game:shoot` / `game:fire` names in `NewNewNewJFMD.md`?
7. `JOIN_LEVEL_ACK`: a socket ack callback or a separate event?
8. Clock sync: ack callback (guide) or the `time_sync_ack` event the client uses?
9. Does a jackpot win also update the winner's balance in the same `shot_result`, or only via `jackpot_win`?
10. ~~Is `variant` still sent?~~ Yes, equal to `type`.
11. ~~Player join/leave broadcasts?~~ Added as `game:player_joined` / `game:player_left`. Payload shape still needed (ideally `userId`, `username`, `seatIndex`, `balance` on join; `userId` on leave).
12. ~~Is `fireIntervalMs` enforced?~~ Yes. ~~Does SHOOT check `aimTarget`?~~ Yes, against the hitbox, plus that `targetFishId` is alive. `targetFishId` in `bullet_fired`: being added.
13. Socket error `Too many request requests`: a per-socket limit on `request` events, separate from `fireIntervalMs`. Measured: tripped at 6 requests in 1 s / 31 in 5 s (laser at 330 ms, FIRE + SHOOT per tick), so about 6/s. Once tripped it keeps refusing (no ack, so FIRE times out) even at 1 request per 1.5 s, for 5+ s. Every shot costs 2 requests, so this caps every gun at ~3 shots/s. **Ask:** leave FIRE/SHOOT out of this limit (they already have `fireIntervalMs`), or raise it to ≥ 10/s. Also: exact count/window, per socket or per event, and how long the block lasts.

## Fish list (`fishesData`, renamed to config `type`)

| Category | Types (old client name) |
|---|---|
| normal | `flying_fish` (small_dragon_fish), `clownfish` (orange_fish), `angelfish` (angel_fish), `blowfish` (pinecone_fish), `pufferfish` (puffer_fish), `sea_turtle` (turtle_fish), `jellyfish` (jelly_fish), `lobster` (lobster_fish), `hermit_crab` (lion_fish), `octopus` (babyocto_fish), `lanternfish` (saw_fish), `manta_ray` (bluewing_fish), `hammerhead_shark` (hammer_head) |
| golden | `golden_angelfish`, `golden_clownfish`, `golden_manta_ray` (golden_wing) |
| special | `armored_fish` (special_armored_fish), `anglerfish` (special_angler_fish), `fortune_terrapin` (special_diamond_turtle) |
| effects | `thunder_shark` (effect_blue_fish), `torpedo_crab` (effect_rockcrab_fish), `sea_anemone` (effect_bubblecrab_fish) |
| immortal | `king_squid` (immo_squid_fish), `golden_shark` (immo_shark_fish), `treasure_bowl` (immo_turtle_fish) |
| jackpot | `jackpot_fish` (jackpot_rainbow_fish), `jackpot_dragon` (jackpot_dragon_fish) |

## Session log
- **2026-11-04**: Removed the Curvy dependency from all scripts ahead of deleting the plugin. Scene/prefab cleanup (SplineController on fish prefabs, `CurvyPathManager`, `_CurvyGlobal_`, `CURVY_SPLINES` define) is left to the user.
- **2026-10-30**: Ported normal gun FIRE/SHOOT to two-phase billing, server-only balance, weapon costs from config, REWARD handling.
- **2026-11-03 (5)**: Measured the request limit (~6/s, see question 13). Pause after the limit doubles per repeat (max 12 s) and resets on the next ack. The error log shows the recent request history.
- **2026-11-03 (4)**: Intervals raised to 250 / 300 / 600 in config. 1.5 s fire pause after the server's request-limit error. Laser cleanup null-guarded (MissingReferenceException on exiting Play mode).
- **2026-11-03 (3)**: Opponent torpedo and laser visuals from `bullet_fired.targetFishId` (`SeatRig.PlayRemoteFire`, `TorpedoGun.PlayRemoteShot`, `LazerGun.PlayRemoteTick`).
- **2026-11-03 (2)**: Fire intervals × 1.1 margin. Every socket-driven log ends with its JSON (errors, timeouts, rejections, joins/leaves, clock, kills/despawns, opponent shots; spawn logs print the fish's own JSON). SHOOT ack without balance is now Info (normal on a miss). Asked backend about the `Too many request requests` limit.
- **2026-11-03**: Torpedo and laser on FIRE/SHOOT (`weaponType`, `targetFishId`), Target Lock / Torpedo buttons live, every gun paced by config `fireIntervalMs`. Opponent rigs: bet +/- hidden, bet from `betIndex`, balance from `shooterBalance`, torpedo/laser not drawn until `bullet_fired` has `targetFishId`. A FIRE ack timeout now cancels the shot like a rejection. Bet change routed through `UIManager.OnBetChanged` for the bet-based art.
- **2026-11-02**: Renamed `fishesData` variants and code literals to config types; removed variant aliases and fallback; created this tracker. Seats confirmed fine with 3. Reviewed the new init shape; `spawnTime` is now `double`. Gun cost fallback 1 / 4 / 1 in code.
