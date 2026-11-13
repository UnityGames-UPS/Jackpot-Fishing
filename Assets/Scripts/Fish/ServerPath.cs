using System;
using System.Collections.Generic;
using UnityEngine;

// A server-authored fish path. The math is a direct port of getDistanceFromKeyframes and
// getSplinePositionAt from the backend's reference client; the server evaluates the same
// functions for hit detection, so this must stay identical (including extrapolation past
// both ends). Positions are in server pixels.
internal sealed class ServerPath
{
  private readonly Vector2[] points;
  private readonly float[] cumulativeLengths;
  private readonly MotionKeyframe[] keyframes;
  private readonly float totalLength;

  internal double SpawnTime { get; }
  internal double Lifespan { get; }

  internal ServerPath(SpawnedFish dto)
  {
    SpawnTime = dto.spawnTime;
    Lifespan = dto.lifespan;

    List<MLFGVec3> src = dto.pathParams?.points;
    int count = src?.Count ?? 0;
    points = new Vector2[count];
    for (int i = 0; i < count; i++)
      points[i] = src[i].ToVector2();

    List<float> lengths = dto.pathParams?.cumulativeLengths;
    cumulativeLengths = lengths != null && lengths.Count == count
      ? lengths.ToArray()
      : ComputeCumulativeLengths(points);
    totalLength = count > 0 ? cumulativeLengths[count - 1] : 0f;

    keyframes = dto.motionKeyframes != null ? dto.motionKeyframes.ToArray() : Array.Empty<MotionKeyframe>();
  }

  internal bool IsValid => points.Length >= 2;

  internal bool IsBeforeSpawn(double nowMs) => nowMs < SpawnTime;

  // 0..1 along the path; < 0 before spawn and > 1 after the end (extrapolated, like the server).
  internal double ProgressAt(double nowMs)
  {
    double elapsed = nowMs - SpawnTime;

    if (keyframes.Length > 1)
      return totalLength > 0 ? GetDistanceFromKeyframes(elapsed) / totalLength : 0;

    return Lifespan > 0 ? elapsed / Lifespan : 1;
  }

  internal Vector2 PositionAtTime(double nowMs) => PositionAt(ProgressAt(nowMs));

  // Distance travelled along the path in px (may be < 0 or > the path length; extrapolated).
  internal double DistanceAt(double nowMs) => ProgressAt(nowMs) * totalLength;

  // Position at an absolute path distance, extrapolated past both ends like PositionAt.
  internal Vector2 PositionAtDistance(double distance) =>
    totalLength > 0 ? PositionAt(distance / totalLength) : PositionAt(0);

  private double GetDistanceFromKeyframes(double elapsedMs)
  {
    if (keyframes.Length == 0) return 0;

    if (elapsedMs <= 0)
    {
      if (keyframes.Length >= 2)
      {
        var a = keyframes[0];
        var b = keyframes[1];
        if (b.t > a.t)
          return a.d + (elapsedMs - a.t) / (b.t - a.t) * (b.d - a.d);
      }
      return keyframes[0].d;
    }

    var last = keyframes[keyframes.Length - 1];
    if (elapsedMs >= last.t)
    {
      if (keyframes.Length >= 2)
      {
        var a = keyframes[keyframes.Length - 2];
        var b = keyframes[keyframes.Length - 1];
        if (b.t > a.t)
          return a.d + (elapsedMs - a.t) / (b.t - a.t) * (b.d - a.d);
      }
      return last.d;
    }

    for (int i = 0; i < keyframes.Length - 1; i++)
    {
      var a = keyframes[i];
      var b = keyframes[i + 1];
      if (elapsedMs >= a.t && elapsedMs < b.t)
        return a.d + (elapsedMs - a.t) / (b.t - a.t) * (b.d - a.d);
    }
    return last.d;
  }

  internal Vector2 PositionAt(double t)
  {
    if (points.Length == 0) return Vector2.zero;
    if (points.Length == 1) return points[0];
    if (totalLength <= 0) return points[0];

    double targetDistance = t * totalLength;

    // Past the end: extrapolate along the last segment.
    if (targetDistance >= totalLength)
      return LerpSegment(points.Length - 2, targetDistance);

    // Before the start: extrapolate backwards along the first segment.
    if (targetDistance <= 0)
      return LerpSegment(0, targetDistance);

    int low = 0;
    int high = cumulativeLengths.Length - 1;
    while (low <= high)
    {
      int mid = (low + high) / 2;
      if (cumulativeLengths[mid] < targetDistance) low = mid + 1;
      else high = mid - 1;
    }

    int i = Math.Max(0, low - 1);
    int nextI = Math.Min(i + 1, points.Length - 1);
    if (i == nextI) return points[i];

    return LerpSegment(i, targetDistance);
  }

  // Unclamped lerp on segment i -> i+1 at an absolute path distance.
  private Vector2 LerpSegment(int i, double targetDistance)
  {
    double segmentStart = cumulativeLengths[i];
    double segmentLen = cumulativeLengths[i + 1] - segmentStart;
    if (segmentLen <= 0)
      return targetDistance <= 0 ? points[i] : points[i + 1];

    float localT = (float)((targetDistance - segmentStart) / segmentLen);
    return points[i] + (points[i + 1] - points[i]) * localT;
  }

  private static float[] ComputeCumulativeLengths(Vector2[] pts)
  {
    var lengths = new float[pts.Length];
    for (int i = 1; i < pts.Length; i++)
      lengths[i] = lengths[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);
    return lengths;
  }
}
