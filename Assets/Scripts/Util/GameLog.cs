using UnityEngine;

internal enum LogCategory
{
  Socket,    // connect / disconnect / heartbeat / request failures
  Payload,   // raw inbound events and outbound requests (verbose)
  Clock,     // time_sync samples and offset
  Fish,      // spawn, kill, leave, release
  Bullet,    // fire and SHOOT resolution (hit / miss)
  Balance,   // ledger reservations, acks, server balance updates
  UI,        // popups, sound, buttons
  Lifecycle, // focus / visibility, orientation, JS bridge, exit
  Legacy     // paused old-backend features
}

// Category-filtered logging. Toggle categories in one place with the LogSettings component.
// Info and Warn respect the toggles; Error is never filtered.
//
// Hot paths (per bullet, per fish, payload dumps) should check IsOn first so the message
// string isn't built when the category is off:
//   if (GameLog.IsOn(LogCategory.Payload)) GameLog.Info(LogCategory.Payload, json.ToString());
internal static class GameLog
{
  private static readonly bool[] enabled = DefaultToggles();
  private static bool alwaysShowWarnings = true;
  private static bool infoEnabled = true;

  internal static bool IsOn(LogCategory category) => infoEnabled && enabled[(int)category];

  internal static void Info(LogCategory category, string message)
  {
    if (IsOn(category))
      Debug.Log(Format(category, message));
  }

  internal static void Warn(LogCategory category, string message)
  {
    if (alwaysShowWarnings || IsOn(category))
      Debug.LogWarning(Format(category, message));
  }

  internal static void Error(LogCategory category, string message)
  {
    Debug.LogError(Format(category, message));
  }

  internal static void Configure(bool[] toggles, bool showWarnings, bool allowInfo)
  {
    for (int i = 0; i < enabled.Length && i < toggles.Length; i++)
      enabled[i] = toggles[i];
    alwaysShowWarnings = showWarnings;
    infoEnabled = allowInfo;
  }

  private static string Format(LogCategory category, string message) => $"[{category}] {message}";

  // Used until a LogSettings component applies its values.
  private static bool[] DefaultToggles()
  {
    var toggles = new bool[System.Enum.GetValues(typeof(LogCategory)).Length];
    toggles[(int)LogCategory.Socket] = true;
    toggles[(int)LogCategory.UI] = true;
    toggles[(int)LogCategory.Lifecycle] = true;
    return toggles;
  }
}
