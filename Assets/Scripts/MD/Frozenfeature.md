# Unity Client Integration Guide: Rolling / Overlapping Screen Freeze

## 1. Overview & Mechanics

The Screen Freeze feature allows players in a multiplayer fish game room to temporarily freeze all swimming fish for **30 seconds**.

### Previous vs New Behavior:
* **Previous Behavior:**
  * When User A froze the room, all other players had their freeze button disabled for the entire 30 seconds.
* **New Behavior (Rolling / Overlapping Freeze):**
  * When **User A** activates freeze, the room freezes for 30s. User A enters a **personal cooldown of 30s**.
  * While the room is frozen, other players (**User B, User C**) can also click Freeze if they have enough balance and are not on their own personal cooldown.
  * When another player activates freeze, the room freeze time **extends dynamically** from the moment of their click:
    $$\text{newEndTime} = \max(\text{currentRoomEndTime}, \text{clickTimestamp} + 30\text{s})$$
  * **Example:**
    * $t = 0\text{s}$: User A clicks Freeze $\rightarrow$ Room frozen until $t = 30\text{s}$. User A cooldown: $0\text{s} - 30\text{s}$.
    * $t = 5\text{s}$: User B clicks Freeze $\rightarrow$ Room frozen until $t = 5\text{s} + 30\text{s} = 35\text{s}$ ($+5\text{s}$ added to room). User B cooldown: $5\text{s} - 35\text{s}$.
    * $t = 25\text{s}$: User C clicks Freeze $\rightarrow$ Room frozen until $t = 25\text{s} + 30\text{s} = 55\text{s}$ ($+20\text{s}$ added to room). User C cooldown: $25\text{s} - 55\text{s}$.
    * $t = 30\text{s}$: User A's 30s cooldown expires $\rightarrow$ User A can click Freeze again!

---

## 2. Server Event Payloads & Schema

### A. Freeze Request (Client $\rightarrow$ Server)
Same event contract as before via `game:request`:
```json
{
  "type": "FREEZE",
  "payload": {
    "roomId": "room_123",
    "requestId": "uuid_v4_string"
  }
}
```

#### Server ACK Response:
```json
{
  "success": true,
  "payload": {
    "balance": 970.0,
    "actionType": "INITIAL", // or "EXTENDED"
    "extendedByMs": 30000,
    "userCooldownExpiresAt": 1731920030000,
    "activeFreeze": {
      "startTime": 1731920000000,
      "endTime": 1731920030000,
      "durationMs": 30000,
      "activatorUserId": "user_1"
    }
  }
}
```
*If user is on cooldown:*
```json
{
  "success": false,
  "payload": {
    "message": "Freeze cooldown active. Please wait before freezing again.",
    "cooldownExpiresAt": 1731920030000
  }
}
```

---

### B. Broadcast: `game:room_frozen` (Server $\rightarrow$ All Room Clients)
Broadcasted whenever **any** user starts or extends room freeze (Optimized clean flat payload):

```json
{
  "roomId": "room_123",
  "activatorUserId": "user_2",
  "actionType": "EXTENDED",        // "INITIAL" (new freeze) or "EXTENDED" (stacked)
  "extendedByMs": 5000,            // How many milliseconds the room was extended by
  "startTime": 1731920000000,      // Room freeze start timestamp
  "endTime": 1731920035000,        // Updated room freeze end timestamp
  "durationMs": 30000,
  "totalDurationMs": 35000,        // Total cumulative duration room has been / will be frozen
  "userCooldownExpiresAt": 1731920035000, // Cooldown for the activator
  "balance": 970.0                 // Activator's updated balance
}
```

---

### C. Broadcast: `game:room_unfrozen` (Server $\rightarrow$ All Room Clients)
Broadcasted when room freeze completely ends (`Date.now() >= endTime`):

```json
{
  "roomId": "room_123",
  "unfrozenAt": 1731920055000,
  "startTime": 1731920000000,
  "totalFrozenMs": 55000          // Exact total milliseconds the room was frozen
}
```

