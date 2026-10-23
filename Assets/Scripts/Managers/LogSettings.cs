using UnityEngine;

// The single place to toggle game logs. Put it on one GameObject in MainScene.
// Changes apply live in Play mode. Errors are always logged regardless of these toggles.
[DefaultExecutionOrder(-1000)]
internal class LogSettings : MonoBehaviour
{
  [Header("Network")]
  [Tooltip("Connect, disconnect, heartbeat, request failures")]
  [SerializeField] private bool socket = true;
  [Tooltip("Raw inbound events and outbound requests. Verbose.")]
  [SerializeField] private bool payloads = false;
  [Tooltip("time_sync samples and clock offset")]
  [SerializeField] private bool clock = false;

  [Header("Gameplay")]
  [Tooltip("Fish spawn, kill, leave, release")]
  [SerializeField] private bool fish = false;
  [Tooltip("Bullet fire and SHOOT resolution")]
  [SerializeField] private bool bullets = false;
  [Tooltip("Balance reservations, acks, server balance")]
  [SerializeField] private bool balance = false;

  [Header("UI & Lifecycle")]
  [SerializeField] private bool ui = true;
  [Tooltip("Focus/visibility, orientation, JS bridge, exit")]
  [SerializeField] private bool lifecycle = true;

  [Header("Paused Features")]
  [SerializeField] private bool legacy = false;

  [Header("General")]
  [Tooltip("Show warnings even when their category is off")]
  [SerializeField] private bool alwaysShowWarnings = true;
  [Tooltip("Allow info logs in non-development builds. WebGL forwards every log to the host app, so keep this off for production.")]
  [SerializeField] private bool infoLogsInReleaseBuilds = false;

  private void Awake() => Apply();

  private void OnValidate() => Apply();

  private void Apply()
  {
    var toggles = new bool[System.Enum.GetValues(typeof(LogCategory)).Length];
    toggles[(int)LogCategory.Socket] = socket;
    toggles[(int)LogCategory.Payload] = payloads;
    toggles[(int)LogCategory.Clock] = clock;
    toggles[(int)LogCategory.Fish] = fish;
    toggles[(int)LogCategory.Bullet] = bullets;
    toggles[(int)LogCategory.Balance] = balance;
    toggles[(int)LogCategory.UI] = ui;
    toggles[(int)LogCategory.Lifecycle] = lifecycle;
    toggles[(int)LogCategory.Legacy] = legacy;

    bool allowInfo = Debug.isDebugBuild || infoLogsInReleaseBuilds;
    GameLog.Configure(toggles, alwaysShowWarnings, allowInfo);
  }
}
