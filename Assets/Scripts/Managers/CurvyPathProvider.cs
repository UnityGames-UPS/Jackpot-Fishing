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

  private void Awake()
  {
    Instance = this;
  }

  public List<PathSet> GetPathSetsForType(FishType type)
  {
    return pathSets.FindAll(p => p.allowedType == type);
  }

  public PathSet GetRandomPathSet(FishType type)
  {
    var sets = GetPathSetsForType(type);
    if (sets == null || sets.Count == 0)
      return null;

    return sets[Random.Range(0, sets.Count)];
  }

  public List<CurvySpline> GetSplinesFromSet(PathSet set, bool moveRightToLeft)
  {
    if (set == null)
      return null;

    return moveRightToLeft
      ? set.rightToLeft
      : set.leftToRight;
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
}

[System.Serializable]
public class PathSet
{
  public string id; // "Set1", "Set2", etc.
  public FishType allowedType;

  public List<CurvySpline> leftToRight = new();
  public List<CurvySpline> rightToLeft = new();
}
