using System;

// Room-wide fish freeze (ML-JF FREEZE). Only fish paths stop: while frozen, fish that spawned
// before the freeze sample their path at StartMs. ServerClock.NowMs keeps running for bullets,
// SHOOT clientTs and everything else. On unfreeze FishManager shifts those fishes' spawnTime by
// the freeze duration so they resume from the same spot (IntegrationGuide §G).
// Not to be confused with BaseFish.movementFrozen, which holds a single fish during its death.
internal static class RoomFreeze
{
  internal static bool IsFrozen { get; private set; }
  internal static double StartMs { get; private set; }
  internal static double EndMs { get; private set; }
  internal static double DurationMs { get; private set; }
  internal static string ActivatorUserId { get; private set; }
  // startTime of the latest freeze applied: the FREEZE ack and game:room_frozen repeat it.
  private static double latestStartMs;

  // The time a fish spawned at spawnTime samples its path at. Fish that spawn during a freeze
  // (not expected) ignore it, or they'd be held before their own spawn and never show.
  internal static double FishTime(double nowMs, double spawnTime) =>
    IsFrozen && spawnTime < StartMs ? Math.Min(nowMs, StartMs) : nowMs;

  internal static bool HasSeen(double startMs) => IsFrozen && (startMs == StartMs || startMs == latestStartMs);

  internal static void Begin(double startMs, double endMs, string activatorUserId)
  {
    IsFrozen = true;
    StartMs = startMs;
    latestStartMs = startMs;
    EndMs = endMs;
    DurationMs = endMs - startMs;
    ActivatorUserId = activatorUserId;
  }

  // A second freeze while frozen (double freeze): fish stay held from the first start until the
  // later end, and the unfreeze shifts them by the whole span.
  internal static void Extend(double startMs, double endMs, string activatorUserId)
  {
    latestStartMs = startMs;
    EndMs = Math.Max(EndMs, endMs);
    DurationMs = EndMs - StartMs;
    ActivatorUserId = activatorUserId;
  }

  internal static void Clear()
  {
    IsFrozen = false;
    StartMs = 0;
    EndMs = 0;
    DurationMs = 0;
    ActivatorUserId = null;
    latestStartMs = 0;
  }
}
