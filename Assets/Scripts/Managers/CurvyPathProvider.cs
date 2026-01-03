using UnityEngine;
using FluffyUnderware.Curvy;
using System.Collections.Generic;

public class CurvyPathProvider : MonoBehaviour
{
  public static CurvyPathProvider Instance;

  [SerializeField] private List<CurvySpline> leftToRightSplines = new();
  [SerializeField] private List<CurvySpline> rightToLeftSplines = new();

  private void Awake()
  {
    Instance = this;
  }

  public CurvySpline GetRandomSpline(bool rightToLeft)
  {
    var list = rightToLeft ? rightToLeftSplines : leftToRightSplines;

    if (list.Count == 0)
      return null;

    return list[Random.Range(0, list.Count)];
  }
}
