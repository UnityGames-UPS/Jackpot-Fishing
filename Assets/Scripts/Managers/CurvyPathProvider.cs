using UnityEngine;
using FluffyUnderware.Curvy;
using System.Collections.Generic;

public class CurvyPathProvider : MonoBehaviour
{
  public static CurvyPathProvider Instance;

  [Header("Authored Path Sets")]
  [SerializeField] private List<PathSet> pathSets = new();

  [Header("Fallback Curves (old behavior)")]
  [SerializeField] private List<CurvySpline> fallbackLeftToRight = new();
  [SerializeField] private List<CurvySpline> fallbackRightToLeft = new();

  private readonly HashSet<CurvySpline> reservedSplines = new();

  private void Awake()
  {
    Instance = this;
  }

  public List<PathSet> GetPathSetsForType(FishType type)
  {
    return pathSets.FindAll(p => p.allowedType == type);
  }

  public List<CurvySpline> GetFallbackSplines(bool moveRightToLeft)
  {
    var list = moveRightToLeft
      ? fallbackRightToLeft
      : fallbackLeftToRight;

    if (list == null || list.Count == 0)
    {
      Debug.LogWarning(
        $"[CurvyPathProvider] Fallback splines missing for " +
        (moveRightToLeft ? "RightToLeft" : "LeftToRight")
      );
    }

    return list;
  }

  internal CurvySpline ReserveUniqueSpline(List<CurvySpline> splines)
  {
    if (splines == null || splines.Count == 0)
      return null;

    List<CurvySpline> available = null;
    for (int i = 0; i < splines.Count; i++)
    {
      var spline = splines[i];
      if (reservedSplines.Contains(spline))
        continue;

      available ??= new List<CurvySpline>();
      available.Add(spline);
    }

    var chosen = (available != null && available.Count > 0)
      ? available[Random.Range(0, available.Count)]
      : splines[Random.Range(0, splines.Count)];

    ReserveSpline(chosen);
    return chosen;
  }

  internal void ReserveSpline(CurvySpline spline)
  {
    if (spline == null)
      return;

    reservedSplines.Add(spline);
  }

  internal void ReleaseSpline(CurvySpline spline)
  {
    if (spline == null)
      return;

    reservedSplines.Remove(spline);
  }

  internal bool IsSplineReserved(CurvySpline spline)
  {
    if (spline == null)
      return false;

    return reservedSplines.Contains(spline);
  }
}

[System.Serializable]
public class PathSet
{
  public string id; // "Set1", "Set2", etc.
  public FishType allowedType;

  public List<CurvySpline> leftToRight = new();
  public List<CurvySpline> rightToLeft = new();
}
