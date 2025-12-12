using UnityEngine;

[CreateAssetMenu(fileName = "PathConfig", menuName = "Fishing/Path Config")]
public class PathConfig : ScriptableObject
{
  [Header("Debug & Control")]
  public bool isEnabled = true;

  [Header("Identification")]
  public string pathId;

  [Header("Pattern")]
  public PathPattern pattern = PathPattern.SineWave;
  public int steps = 20;

  [Header("Start/End Points (Grid Coordinates)")]
  public int startX = 0;
  public int startY = 3;
  public int endX = 10;
  public int endY = 3;

  [Header("Wave Settings")]
  [Range(0.5f, 10f)] public float frequency = 2f;
  [Range(0.5f, 5f)] public float amplitude = 1.5f;

  // Trigger path regeneration when values change in inspector
#if UNITY_EDITOR
  private void OnValidate()
  {
    if (!Application.isPlaying)
    {
      UnityEditor.EditorApplication.delayCall += () =>
      {
        var manager = UnityEngine.Object.FindObjectOfType<PathManager>();
        if (manager != null)
        {
          manager.RegeneratePaths();
        }
      };
    }
  }
#endif

}

public enum PathPattern
{
  Straight,
  SineWave
}
