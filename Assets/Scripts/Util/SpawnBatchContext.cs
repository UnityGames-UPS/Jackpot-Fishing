using System.Collections.Generic;
using FluffyUnderware.Curvy;

public class SpawnBatchContext
{
  // Intent (decided once)
  public bool moveRightToLeft;
  public bool usePathSet;

  // Only used if usePathSet == true
  public PathSet chosenPathSet;

  // Usage tracking
  private HashSet<CurvySpline> usedSplines = new();

  public bool IsSplineUsed(CurvySpline spline)
    => usedSplines.Contains(spline);

  public void MarkSplineUsed(CurvySpline spline)
    => usedSplines.Add(spline);
}


