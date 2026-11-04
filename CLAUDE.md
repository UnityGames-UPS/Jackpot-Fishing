# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

This is **Jackpot Fishing**, a multiplayer fish-shooting game built in Unity **6000.3.24f1**. It ships as a WebGL build that runs inside a React Native WebView or a web iframe.

The current client was written against an **old backend**. The plan:
1. The user will provide a **new backend spec**. Port the client to it, and pause features the new backend doesn't support yet. Disable or stub them; don't delete them.
2. After the backend catches up, build out the full Jackpot Fishing feature set.

Some art and animation assets are still **pending**, so work with what exists and leave clear hooks where assets are missing.

The new backend is **ML-JF** (server-authoritative). Its live config is `Assets/Scripts/JSONS/Config.json`, and its docs are in `Assets/Scripts/MD/` (`JackpotFishing_IntegrationGuide.md`, `moreFeat.md`, `NewNewNewJFMD.md`). When they disagree, the config wins, but treat whatever the user provides as authoritative.

**`BACKEND_PORT.md` (repo root) tracks what is done, what is left and open backend questions.** Read it before backend work, and update it at the end of every session: move finished items, add new ones, and add a line to its session log.

## Edit permissions

| Area | Rule |
|---|---|
| `Assets/Scripts/**`, `Assets/HttpServices/**` | Free to read and edit |
| `Assets/Plugins/WebGL/CustomJsLib.jslib`, any `index.html` / WebGL template files | Free to read and edit |
| Prefabs (`Assets/Prefabs/**`) and `Assets/Scenes/MainScene.unity` | **Read only. Ask before modifying** |
| `Assets/Plugins/**` (DOTween, Best HTTP/SocketIO, etc.) | Read only |
| Everything else (folders, ProjectSettings, Packages) | Read to understand structure; ask before changing |

If a script change needs a new serialized field to be wired up in a prefab or scene, make the script change and tell the user what to hook up in the Inspector. Don't edit the YAML yourself.

There is no `index.html` in the repo right now. `ProjectSettings` uses the built-in `APPLICATION:Minimal` WebGL template. A custom template would go in `Assets/WebGLTemplates/`.

## Building and testing

This is a Unity project with no CLI build scripts, linter or test suite. Builds and Play-mode runs happen in the Unity Editor (target: WebGL). The `.csproj`/`.slnx` files are Unity-generated, so don't edit them.

**Compile checks:** don't try to compile from the CLI. After making script changes, ask the user to check compilation in the Unity Editor and wait for them to confirm (or paste errors) before moving on.

Editor-only testing paths:
- `FishManager` has mock spawning in `#if UNITY_EDITOR`: press **M** to spawn `fishesData[mockFishIndex]` on a mock server path and **K** to kill a random fish, with no backend needed.
- In the Editor, `SocketIOManager` connects to `TestSocketURI` with the `testToken` serialized field. In a WebGL build it requests the URL and token from the host page instead (see below).

## Architecture

### Singletons and the scene
Everything lives in `MainScene.unity`. Managers are MonoBehaviours with `internal static X Instance` set in `Awake`, and they call each other directly through those singletons:
- `SocketIOManager`: networking, response parsing, and the orchestration of hit and kill results. This is where most backend porting work will happen.
- `FishManager`: per-type fish pools, `FishData` templates keyed by `variant` (which must equal the server's fish `type`), active fish list, and coin and star-coin effect scaling.
- `UIManager`: balance, bet index (`BetCounter`), gun selection buttons, popups (reconnect, disconnect, low balance) and win animations.
- `GunManager`: owns `BaseGun[] Guns` and `currentGun`; does aiming and firing toggles.
- `InputManagerView`: pointer events → aim and fire, plus picking the fish under the pointer for target lock.
- `JSFunctCalls`: C# side of the JS bridge.

### Network flow (old backend, paused)
The legacy code lives in `Assets/Scripts/Network/Legacy/` and `BaseFish.Legacy.cs`. The ML-JF flow is in `SocketIOManager` and `Network/MLFGDtos.cs`; see `BACKEND_PORT.md`.
Uses Best.SocketIO (`Assets/Plugins`, `com.Tivadar.Best.*`) over WebSocket on namespace `/playground`, with token auth.
- Inbound events are `game:init` and `result`, both routed to `ParseResponse`, which switches on `root.id`: `initdata` / `spawnresult` / `hitresult`. There is also `pong`.
- Outbound events are `request` (spawn request and hit events via `SendHitEvent(fishId, weaponType, variant)`) and `ping`, all sent through `SendDataWithNamespace`.
- `HandleSpawnResult` maps the backend `Fish` → `FishManager.ToFishData` (the variant lookup in `fishesData`; `lifespan` becomes `duration`) → `SpawnFishFromBackend`. The old client-side Curvy paths have been removed, so legacy-spawned fish don't move. Fish move only on the server's `ServerPath`.
- `HandleHitResult` holds most of the game feel: weapon-specific branches (`"torpedo"`, `"lazer"`), effect-fish chains (bubble crab, blue fish, rock crab), immortal fish bucket animations, rainbow and star-coin wins, and refunds. It decides on variant string literals like `immo_turtle_fish` and `effect_rockcrab_fish`.
- DTOs (`Root`, `GameData`, `Fish`, `HitPayload`, ...) are at the bottom of `SocketIOManager.cs` and use Newtonsoft JSON.
- Ping and pong do the heartbeat: 15 missed pongs → disconnect popup. Losing app focus starts a 180 s disconnect timer.
- `Assets/HttpServices` holds a generic HTTP service layer that the game scripts don't use right now.

### WebGL ↔ host bridge
`CustomJsLib.jslib` ↔ `JSFunctCalls`:
- `SendPostMessage("authToken")` reads `window.ReactNativeWebView.injectedObjectJson()` (`socketURL`, `token`, `nameSpace`) and calls back `SendMessage('SocketManager', 'ReceiveAuthToken', json)`. **The GameObject must stay named `SocketManager`.**
- Other messages are `OnEnter` and `error`. They go to the RN WebView, or to `window.parent.dispatchReactUnityEvent` when the game runs in an iframe.
- In WebGL builds, every Unity log is forwarded to RN through `SendLogToReactNative`.

### Fish
`BaseFish` handles server-path movement (`ServerPath`), sprite-frame animation, viewport checks, death and despawn, and torpedo bookkeeping: incoming-torpedo counts and pending death until the killing torpedo arrives. Subclasses map one-to-one to `FishType`: `NormalFish`, `GoldenFish`, `SpecialFish`, `EffectFish` (large, holds the chain and area effects), `ImmortalFish`, `JackpotFish` and `JackpotDragon`. `FishData` assets (sprites, sizes, collider) are serialized on `FishManager` and matched by `variant`, which is the server's fish `type` (e.g. `king_squid`). Code that branches on a fish uses these names too.

### Guns
`BaseGun` (abstract `Fire()`, `UpdateAim`) → `SimpleGun` (normal bullets that bounce), `TorpedoGun` (lock-on; FIRE on launch, SHOOT on impact through `TorpedoShot`), `LazerGun` (continuous beam; one FIRE + SHOOT per tick). All three use `SocketIOManager.SendFire` / `SendShoot` with their `weaponType` (`MLFGWeaponIds`), and each waits `FireIntervalFor(weapon)` (config `fireIntervalMs`, which the server enforces) between shots. `SocketIOManager.GunCosts` (index 0 normal, 1 torpedo, 2 laser) holds bet multipliers and is overwritten from `weapons.*.cost` on init. Opponent shots (`game:bullet_fired`) are visual only: `SeatRig.PlayRemoteFire` switches to the fired gun, and torpedo/laser aim at `targetFishId`.

### Pooling
Every spawnable thing (fish of each type, bullets, VFX, popups) goes through `GenericObjectPool<T>`. Because Unity can't serialize generic MonoBehaviours, each type has a one-line concrete wrapper in `Assets/Scripts/Util/WrapperPoolScripts/`, for example `internal class NormalFishPool : GenericObjectPool<NormalFish> { }`. Add a new wrapper for each new pooled type, and return objects with `ReturnToPool` rather than `Destroy`.

### Conventions
- Two-space indentation.
- Access modifiers: use the narrowest one. `private` by default, `internal` for members used by other classes, and `public` only when it's actually needed (e.g. Unity interface implementations like `IPointerDownHandler`, `[Serializable]` DTO fields). Methods called from JS via `SendMessage` don't need to be `public`, because `SendMessage` finds them by name through reflection and works on private methods (see `ReceiveAuthToken`).
- DOTween (`DOTween.defaultTimeScaleIndependent = true`) handles tweens. Sprite animations use `ImageAnimation`.
- Death and VFX sequencing is chained through callbacks such as `fish.OnFishDespawned = () => ...` and torpedo-arrival waits, not events. Keep that ordering when you refactor hit handling.
