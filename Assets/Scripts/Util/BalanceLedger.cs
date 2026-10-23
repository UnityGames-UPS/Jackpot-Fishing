using System.Collections.Generic;
using UnityEngine;

// Displayed balance = last server balance - cost of bullets the server hasn't acknowledged yet.
// Firing deducts instantly (reserve); the SHOOT ack carries the authoritative balance that
// already includes that bullet, so the reservation is dropped at the same moment. This keeps
// rapid fire from flickering the balance back up while acks are in flight.
internal class BalanceLedger
{
  private const float PendingTimeoutSeconds = 20f; // > bullet max lifetime + ack timeout

  private struct Pending
  {
    internal double cost;
    internal float reservedAt;
  }

  private readonly Dictionary<string, Pending> pending = new Dictionary<string, Pending>();
  private readonly List<string> expired = new List<string>();
  private double serverBalance;
  private double pendingTotal;

  internal double Available => serverBalance - pendingTotal;
  internal int PendingCount => pending.Count;

  internal bool TryReserve(string bulletId, double cost)
  {
    ExpireStale();
    if (cost <= 0 || Available < cost)
      return false;

    pending[bulletId] = new Pending { cost = cost, reservedAt = Time.unscaledTime };
    pendingTotal += cost;
    return true;
  }

  // SHOOT ack: the server balance after this bullet was charged.
  internal void Confirm(string bulletId, double? balance)
  {
    Drop(bulletId);
    if (balance.HasValue)
      serverBalance = balance.Value;
  }

  // The bullet will never be charged (e.g. it could not be sent).
  internal void Cancel(string bulletId)
  {
    Drop(bulletId);
  }

  // Authoritative balance pushed by the server (init, own shot_result, balance:sync).
  internal void SetServerBalance(double balance)
  {
    serverBalance = balance;
  }

  internal void Clear()
  {
    pending.Clear();
    pendingTotal = 0;
  }

  // A lost ack must not keep the balance reduced forever.
  private void ExpireStale()
  {
    if (pending.Count == 0)
      return;

    float now = Time.unscaledTime;
    expired.Clear();
    foreach (var entry in pending)
    {
      if (now - entry.Value.reservedAt > PendingTimeoutSeconds)
        expired.Add(entry.Key);
    }

    foreach (var id in expired)
    {
      GameLog.Warn(LogCategory.Balance, $"No SHOOT ack for {id} after {PendingTimeoutSeconds}s; releasing reservation");
      Drop(id);
    }
  }

  private void Drop(string bulletId)
  {
    if (string.IsNullOrEmpty(bulletId) || !pending.TryGetValue(bulletId, out var p))
      return;

    pending.Remove(bulletId);
    pendingTotal -= p.cost;
    if (pending.Count == 0)
      pendingTotal = 0; // clear accumulated float error
  }
}
