using System;
using System.Collections.Generic;
using UnityEngine;

// Server time estimate built from time_sync ping-pongs. Never use device time for gameplay:
// fish positions and SHOOT clientTs both come from NowMs.
internal static class ServerClock
{
  private const int SampleWindow = 5;
  private const double SlewMsPerSecond = 50.0;  // max drift correction speed, so fish never visibly jump
  private const double SnapThresholdMs = 1000.0; // larger errors (first sync, device sleep) snap instead

  private struct Sample
  {
    internal double rtt;
    internal double offset;
  }

  private static readonly Queue<Sample> samples = new Queue<Sample>();
  private static double bootUnixMs = double.NaN;
  private static double offset;
  private static double targetOffset;

  internal static bool IsSynced { get; private set; }
  internal static double OffsetMs => offset;
  internal static double LastRttMs { get; private set; }

  // Monotonic local clock in unix ms. realtimeSinceStartup keeps running while the tab is hidden.
  internal static double LocalNowMs
  {
    get
    {
      if (double.IsNaN(bootUnixMs))
        bootUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Time.realtimeSinceStartupAsDouble * 1000.0;
      return bootUnixMs + Time.realtimeSinceStartupAsDouble * 1000.0;
    }
  }

  internal static double NowMs => LocalNowMs + offset;

  // clientTime is the LocalNowMs value we sent with time_sync, echoed back by the server.
  internal static void AddSample(long clientTime, long serverTime)
  {
    double now = LocalNowMs;
    double rtt = now - clientTime;
    if (rtt < 0)
      return;
    LastRttMs = rtt;

    samples.Enqueue(new Sample { rtt = rtt, offset = serverTime - now + rtt / 2.0 });
    while (samples.Count > SampleWindow)
      samples.Dequeue();

    // The lowest round-trip sample has the least asymmetric-latency error.
    Sample best = default;
    best.rtt = double.MaxValue;
    foreach (var s in samples)
    {
      if (s.rtt < best.rtt)
        best = s;
    }
    targetOffset = best.offset;

    if (!IsSynced || Math.Abs(targetOffset - offset) > SnapThresholdMs)
      offset = targetOffset;
    IsSynced = true;
  }

  internal static void Tick(float unscaledDeltaTime)
  {
    if (!IsSynced)
      return;

    double maxStep = SlewMsPerSecond * unscaledDeltaTime;
    double diff = targetOffset - offset;
    offset += Math.Max(-maxStep, Math.Min(maxStep, diff));
  }

  internal static void Reset()
  {
    samples.Clear();
    offset = 0;
    targetOffset = 0;
    IsSynced = false;
  }
}
