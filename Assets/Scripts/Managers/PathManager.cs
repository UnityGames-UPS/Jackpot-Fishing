using System.Collections.Generic;
using UnityEngine;

internal class PathManager : MonoBehaviour
{
  internal static PathManager Instance;

  [Header("Grid Setup")]
  [SerializeField] private Transform gridRoot;
  [SerializeField] private int columns = 12;
  [SerializeField] private int rows = 8;

  private Transform[,] grid;
  internal int gridWidth => columns;
  internal int gridHeight => rows;

  [Header("Path Configurations")]
  [SerializeField] private List<PathConfig> pathConfigs;

  private Dictionary<string, Transform[]> _generatedPaths;

  [Header("Gizmos")]
  [SerializeField] private bool showGizmos = true;
  [SerializeField] private bool showStraightPaths = true;
  [SerializeField] private bool showCurvedPaths = true;
  [SerializeField] private Color straightPathColor = Color.green;
  [SerializeField] private Color curvedPathColor = Color.yellow;
  [SerializeField] private int gizmoResolution = 20;

  private void Awake()
  {
    Instance = this;
    BuildGrid();
    GenerateAllPaths();
  }

#if UNITY_EDITOR
  private void OnValidate()
  {
    if (!Application.isPlaying)
    {
      if (grid == null)
        BuildGrid();
      GenerateAllPaths();
    }
  }

  internal void RegeneratePaths()
  {
    if (grid == null)
      BuildGrid();
    GenerateAllPaths();

    UnityEditor.SceneView.RepaintAll();
  }
#endif

  private void BuildGrid()
  {
    if (gridRoot == null) return;

    grid = new Transform[columns, rows];
    for (int i = 0; i < gridRoot.childCount; i++)
    {
      int x = i % columns;
      int y = i / columns;
      if (y >= rows) continue;
      grid[x, y] = gridRoot.GetChild(i);
    }
  }

  private void GenerateAllPaths()
  {
    if (grid == null) return;
    _generatedPaths = new();

    foreach (var config in pathConfigs)
    {
      if (config == null || !config.isEnabled) continue;
      var path = GeneratePath(config);
      if (path != null && path.Length > 0)
      {
        _generatedPaths[config.pathId] = path;
      }
    }
  }

  private Transform[] GeneratePath(PathConfig config)
  {
    List<Vector3> waypoints = config.pattern switch
    {
      PathPattern.Straight => GenerateStraight(config),
      PathPattern.SineWave => GenerateSineWave(config),
      _ => new List<Vector3>()
    };

    return ConvertToGridTransforms(waypoints);
  }

  private List<Vector3> GenerateStraight(PathConfig c)
  {
    var points = new List<Vector3>();
    Vector3 start = GetGridPosition(c.startX, c.startY);
    Vector3 end = GetGridPosition(c.endX, c.endY);

    for (int i = 0; i < c.steps; i++)
    {
      float t = i / (float)(c.steps - 1);
      points.Add(Vector3.Lerp(start, end, t));
    }
    return points;
  }

  private List<Vector3> GenerateSineWave(PathConfig c)
  {
    var points = new List<Vector3>();
    Vector3 start = GetGridPosition(c.startX, c.startY);
    Vector3 end = GetGridPosition(c.endX, c.endY);
    Vector3 dir = end - start;
    Vector3 perpendicular = new Vector3(-dir.y, dir.x, 0).normalized;

    for (int i = 0; i < c.steps; i++)
    {
      float t = i / (float)(c.steps - 1);
      Vector3 basePos = Vector3.Lerp(start, end, t);
      float wave = Mathf.Sin(t * c.frequency * Mathf.PI * 2) * c.amplitude;
      points.Add(basePos + perpendicular * wave);
    }
    return points;
  }

  private Vector3 GetGridPosition(int x, int y)
  {
    x = Mathf.Clamp(x, 0, columns - 1);
    y = Mathf.Clamp(y, 0, rows - 1);
    return grid[x, y].position;
  }

  private Transform[] ConvertToGridTransforms(List<Vector3> positions)
  {
    var transforms = new List<Transform>();

    foreach (var pos in positions)
    {
      Transform closest = FindClosestGridPoint(pos);
      if (closest != null && !transforms.Contains(closest))
      {
        transforms.Add(closest);
      }
    }

    return transforms.ToArray();
  }

  private Transform FindClosestGridPoint(Vector3 position)
  {
    Transform closest = null;
    float minDist = float.MaxValue;

    for (int x = 0; x < columns; x++)
    {
      for (int y = 0; y < rows; y++)
      {
        if (grid[x, y] == null) continue;

        float dist = Vector3.Distance(position, grid[x, y].position);
        if (dist < minDist)
        {
          minDist = dist;
          closest = grid[x, y];
        }
      }
    }

    return closest;
  }

  internal Transform[] GetPath(string pathId)
  {
    if (_generatedPaths != null && _generatedPaths.TryGetValue(pathId, out var path))
      return path;

    Debug.LogWarning($"Path ID '{pathId}' not found.");
    return null;
  }

  internal Transform[] GetRandomPath()
  {
    if (_generatedPaths.Count == 0) return null;

    int index = Random.Range(0, _generatedPaths.Count);
    int i = 0;

    foreach (var path in _generatedPaths.Values)
    {
      if (i == index)
      {
        return path;
      }
      i++;
    }
    return null;
  }

#if UNITY_EDITOR
  private void OnDrawGizmos()
  {
    if (!showGizmos || _generatedPaths == null) return;

    foreach (var kvp in _generatedPaths)
    {
      var config = pathConfigs.Find(p => p.pathId == kvp.Key);
      if (config == null) continue;

      if (config.pattern == PathPattern.Straight && showStraightPaths)
        Gizmos.color = straightPathColor;
      else if (config.pattern == PathPattern.SineWave && showCurvedPaths)
        Gizmos.color = curvedPathColor;
      else
        continue;

      DrawPath(kvp.Value);
    }
  }

  private void DrawPath(Transform[] waypoints)
  {
    if (waypoints == null || waypoints.Length < 2) return;

    Vector3[] points = new Vector3[waypoints.Length];
    for (int i = 0; i < waypoints.Length; i++)
    {
      points[i] = waypoints[i].position;
      Gizmos.DrawWireSphere(points[i], 0.1f);
    }

    DrawCatmullRomPath(points, gizmoResolution);
  }

  private void DrawCatmullRomPath(Vector3[] points, int resolution)
  {
    for (int i = 0; i < points.Length - 1; i++)
    {
      Vector3 p0 = i == 0 ? points[i] : points[i - 1];
      Vector3 p1 = points[i];
      Vector3 p2 = points[i + 1];
      Vector3 p3 = (i + 2 < points.Length) ? points[i + 2] : points[i + 1];

      Vector3 lastPos = p1;
      for (int j = 1; j <= resolution; j++)
      {
        float t = j / (float)resolution;
        Vector3 newPos = GetCatmullRomPosition(t, p0, p1, p2, p3);
        Gizmos.DrawLine(lastPos, newPos);
        lastPos = newPos;
      }
    }
  }

  private Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
  {
    return 0.5f * (
        2f * p1 +
        (-p0 + p2) * t +
        (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
        (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t
    );
  }
#endif
}
