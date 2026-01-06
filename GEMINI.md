# Jackpot Fishing - Project Context

## Project Overview
**Jackpot Fishing** is a Unity-based 2D fishing game designed primarily for WebGL deployment. It features real-time multiplayer capabilities using Socket.IO, server-authoritative gameplay logic (fish spawning), and tight integration with a web/native wrapper (React Native/Web) via JavaScript interop.

## Key Technologies
*   **Engine:** Unity (2D)
*   **Networking:**
    *   **Socket.IO:** `Best HTTP` / `Best SocketIO` assets for real-time game state and events.
    *   **HTTP:** Custom `Duck.Http` wrapper around UnityWebRequest for REST API calls.
*   **Serialization:** `Newtonsoft.Json` for JSON parsing.
*   **Animation:** `DOTween` for programmatic animations.
*   **Architecture:** Manager-based (Monobehaviour singletons), Object Pooling for performance.

## Architecture & Core Systems

### 1. Managers (`Assets/Scripts/Managers/`)
The game logic is centralized in specific manager classes:
*   **`SocketIOManager.cs`:** The core networking hub.
    *   Handles connection lifecycle (Connect, Disconnect, Reconnect).
    *   Manages Authentication (Token exchange).
    *   Listens for game events: `game:init` (initial data), `result` (spawn/action results), `pong` (latency checks).
    *   Sends events: `ping`, `request` (e.g., spawn requests).
*   **`FishManager.cs`:** Controls fish entities.
    *   Uses **Object Pooling** (`GenericObjectPool`) for efficient spawning/despawning of various fish types (`Normal`, `Golden`, `Special`, `Jackpot`, etc.).
    *   Parses backend fish data into visual representations (`FishData`).
*   **`JSFunctCalls.cs`:** Handles WebGL-specific communication.
    *   `SendCustomMessage`: Sends messages to the embedding web page/app.
    *   `SendLogToReactNative`: Forwards Unity logs to the wrapper console.
*   **`UIManager.cs`:** (Inferred) Manages UI popups, disconnection screens, etc.

### 2. Networking Flow
*   **Connection:**
    *   **Editor:** Uses a hardcoded `testToken`.
    *   **WebGL:** Waits for an `authToken` via `JSManager` before connecting.
*   **Game Loop:**
    1.  Socket Connects -> `OnConnected`.
    2.  Server sends `game:init` -> Client processes `initdata`.
    3.  Client requests fish -> `ReqFishSpawn`.
    4.  Server responds with `spawnresult` (batches of fish).
    5.  Client runs `SpawnFlow` to visually spawn fish at correct timestamps.

### 3. Object Pooling
*   Fish are not instantiated/destroyed frequently. Instead, they are retrieved from and returned to pools (e.g., `normalFishPool`, `goldenFishPool`) defined in `FishManager`.

## Directory Structure
*   `Assets/Scripts/`: Core C# source code.
    *   `Managers/`: Game control logic.
    *   `Fish/`: Fish behaviors and data structures.
    *   `Gun/`: Player weapon logic.
*   `Assets/HttpServices/`: HTTP networking layer.
*   `Assets/Plugins/`: Third-party SDKs (Demigiant/DOTween).
*   `Packages/`: Unity Package Manager dependencies.

## Build & Run Guidelines
*   **Platform:** The project logic heavily relies on `UNITY_WEBGL` defines.
*   **Editor Testing:**
    *   Ensure `TestSocketURI` and `testToken` in `SocketIOManager` are valid for local debugging.
    *   The `JSFunctCalls` logic is stripped out in the Editor to prevent DLL errors.
*   **WebGL Build:**
    *   Requires a wrapper to feed the `authToken`.
    *   Logs are redirected to the wrapper via `SendLogToReactNative`.

## Development Conventions
*   **Serialization:** Use `[Serializable]` classes for API payloads (e.g., `Root`, `Payload`, `Fish`).
*   **Async/Coroutines:** Heavy use of Coroutines for timing (spawning batches, ping intervals, disconnect timers).
*   **Code Style:** Standard C# with PascalCase for methods/classes and camelCase for private fields.
