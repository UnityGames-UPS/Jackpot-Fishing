using System;
using System.IO;
using UnityEngine;

public static class DesktopLogger
{
  private static string FilePath =>
      Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
          "unity_spawn_log.txt"
      );

  public static void Log(string message)
  {
    try
    {
      File.AppendAllText(
          FilePath,
          $"{DateTime.Now:HH:mm:ss.fff}\n{message}\n\n"
      );
    }
    catch (Exception e)
    {
      Debug.LogError("DesktopLogger failed: " + e.Message);
    }
  }
}
