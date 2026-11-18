using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The hall (level) switcher drop-down: shows the current hall, offers the other halls, confirms,
// then sends JOIN_LEVEL behind a loading screen. Opens on every game:init (first join and after
// each switch) and auto-closes after autoCloseDuration.
public class HallSwitcherManager : MonoBehaviour
{
  internal static HallSwitcherManager Instance;

  [Header("Header")]
  [SerializeField] private TMP_Text currentHallText;
  [SerializeField] private Button toggleButton; // flipped on Y while open

  [Header("Panel")]
  [SerializeField] private RectTransform panel;
  [SerializeField] private TMP_Text titleText; // only visible once the panel is fully open
  [Tooltip("One per hall other than the current one, relabelled on every init.")]
  [SerializeField] private Button[] hallButtons;
  [SerializeField] private TMP_Text[] hallButtonTexts; // same order as hallButtons

  [Header("Confirm Popup")]
  [SerializeField] private GameObject confirmPopup;
  [SerializeField] private TMP_Text confirmMessageText;
  [SerializeField] private Button confirmYesButton;
  [SerializeField] private Button confirmNoButton;
  [Tooltip("Invisible full-screen button inside the popup; a click on it closes the popup like No.")]
  [SerializeField] private Button confirmBackgroundButton;

  [Header("Loading")]
  [Tooltip("Black full-screen panel above all UI, shown while switching halls.")]
  [SerializeField] private GameObject loadingPanel;

  [Header("Timing (seconds)")]
  [SerializeField] private float autoCloseDuration = 4f;
  [SerializeField] private float openDuration = 0.4f;
  [SerializeField] private float closeDuration = 0.3f;
  [SerializeField] private float titleFadeDuration = 0.2f;
  [SerializeField] private float minLoadingDuration = 1f;
  [Tooltip("Gives up on a switch if the ack and the new game:init haven't both arrived by then.")]
  [SerializeField] private float switchTimeout = 10f;

  private static readonly string[] DefaultHallOrder = { MLFGHallIds.Joy, MLFGHallIds.Regal, MLFGHallIds.RegalSingle };

  private Dictionary<string, MLFGHall> halls;
  private string currentHall;
  private readonly string[] buttonHallIds = new string[8];
  private string pendingHall; // hall in the confirm popup

  private Vector3 toggleClosedScale = Vector3.one;
  private bool isOpen;
  private Tween panelTween;
  private Tween titleTween;
  private Tween autoCloseTween;

  // In-flight switch: the loading panel hides once all three are true.
  private bool isSwitching;
  private bool switchMinElapsed;
  private bool switchAcked;
  private bool switchInitReceived;
  private Tween switchMinTween;
  private Tween switchTimeoutTween;

  private void Awake()
  {
    Instance = this;

    if (toggleButton)
    {
      Vector3 s = toggleButton.transform.localScale;
      toggleClosedScale = new Vector3(s.x, Mathf.Abs(s.y), s.z);
      toggleButton.onClick.RemoveAllListeners();
      toggleButton.onClick.AddListener(OnClickToggle);
    }

    if (hallButtons != null)
    {
      for (int i = 0; i < hallButtons.Length; i++)
      {
        if (!hallButtons[i]) continue;
        int index = i;
        hallButtons[i].onClick.RemoveAllListeners();
        hallButtons[i].onClick.AddListener(() => OnClickHall(index));
      }
    }

    if (confirmYesButton)
    {
      confirmYesButton.onClick.RemoveAllListeners();
      confirmYesButton.onClick.AddListener(OnClickConfirmYes);
    }
    if (confirmNoButton)
    {
      confirmNoButton.onClick.RemoveAllListeners();
      confirmNoButton.onClick.AddListener(OnClickConfirmNo);
    }
    if (confirmBackgroundButton)
    {
      confirmBackgroundButton.onClick.RemoveAllListeners();
      confirmBackgroundButton.onClick.AddListener(OnClickConfirmNo);
    }

    SnapClosed();
    if (confirmPopup) confirmPopup.SetActive(false);
    if (loadingPanel) loadingPanel.SetActive(false);
  }

  private void OnDestroy()
  {
    panelTween?.Kill();
    titleTween?.Kill();
    autoCloseTween?.Kill();
    switchMinTween?.Kill();
    switchTimeoutTween?.Kill();
  }

  // --------------------------------------------------------
  // INIT
  // --------------------------------------------------------

  // Every game:init: the first join, a reconnect, or the init that follows a JOIN_LEVEL.
  internal void OnInit(string hall, Dictionary<string, MLFGHall> hallList, List<string> levels)
  {
    if (hallList != null && hallList.Count > 0)
      halls = hallList;
    if (!string.IsNullOrEmpty(hall))
      currentHall = hall;
    else
      GameLog.Warn(LogCategory.Socket, "game:init has no currentHall");

    if (currentHallText)
      currentHallText.text = HallName(currentHall);
    BindHallButtons(levels);

    if (isSwitching)
    {
      switchInitReceived = true;
      TryFinishSwitch();
    }
    else
    {
      Open();
    }
  }

  // The buttons offer every hall except the current one, in levels order.
  private void BindHallButtons(List<string> levels)
  {
    if (hallButtons == null)
      return;

    var order = new List<string>();
    if (levels != null && levels.Count > 0)
      order.AddRange(levels);
    else if (halls != null)
      order.AddRange(halls.Keys);
    else
      order.AddRange(DefaultHallOrder);
    order.Remove(currentHall);

    if (order.Count != hallButtons.Length)
      GameLog.Warn(LogCategory.Socket, $"Hall switcher has {hallButtons.Length} buttons for {order.Count} other halls ({string.Join(", ", order)})");

    for (int i = 0; i < hallButtons.Length; i++)
    {
      bool used = i < order.Count && i < buttonHallIds.Length;
      if (i < buttonHallIds.Length)
        buttonHallIds[i] = used ? order[i] : null;
      if (hallButtons[i])
        hallButtons[i].gameObject.SetActive(used);
      if (used && hallButtonTexts != null && i < hallButtonTexts.Length && hallButtonTexts[i])
        hallButtonTexts[i].text = HallName(order[i]);
    }
  }

  private string HallName(string hallId)
  {
    if (string.IsNullOrEmpty(hallId))
      return "";
    if (halls != null && halls.TryGetValue(hallId, out var hall) && hall != null && !string.IsNullOrEmpty(hall.name))
      return hall.name;
    return hallId;
  }

  // --------------------------------------------------------
  // PANEL
  // --------------------------------------------------------

  private void OnClickToggle()
  {
    if (isSwitching || IsConfirmOpen)
      return;

    if (isOpen) Close();
    else Open();
  }

  private bool IsConfirmOpen => confirmPopup && confirmPopup.activeSelf;

  private void Open()
  {
    if (!panel)
      return;

    // Already open (e.g. a game:init right after the JOIN_LEVEL ack): keep it, restart the timer.
    if (isOpen)
    {
      if (!IsConfirmOpen)
        StartAutoClose();
      return;
    }

    isOpen = true;
    panelTween?.Kill();
    titleTween?.Kill();

    if (toggleButton)
      toggleButton.transform.localScale = new Vector3(toggleClosedScale.x, -toggleClosedScale.y, toggleClosedScale.z);
    SetTitleAlpha(0f);

    panel.gameObject.SetActive(true);
    panel.localScale = new Vector3(1f, 0f, 1f);
    panelTween = panel.DOScaleY(1f, openDuration)
      .SetEase(Ease.Linear)
      .OnComplete(() =>
      {
        if (titleText)
          titleTween = DOTween.To(() => titleText.alpha, a => titleText.alpha = a, 1f, titleFadeDuration);
      });

    StartAutoClose();
  }

  private void Close()
  {
    isOpen = false;
    autoCloseTween?.Kill();
    panelTween?.Kill();
    titleTween?.Kill();

    if (toggleButton)
      toggleButton.transform.localScale = toggleClosedScale;
    SetTitleAlpha(0f);

    if (!panel)
      return;

    panelTween = panel.DOScaleY(0f, closeDuration)
      .SetEase(Ease.Linear)
      .OnComplete(() => panel.gameObject.SetActive(false));
  }

  private void SnapClosed()
  {
    isOpen = false;
    if (toggleButton)
      toggleButton.transform.localScale = toggleClosedScale;
    SetTitleAlpha(0f);
    if (panel)
    {
      panel.localScale = new Vector3(1f, 0f, 1f);
      panel.gameObject.SetActive(false);
    }
  }

  private void SetTitleAlpha(float alpha)
  {
    if (titleText)
      titleText.alpha = alpha;
  }

  private void StartAutoClose()
  {
    autoCloseTween?.Kill();
    autoCloseTween = DOVirtual.DelayedCall(autoCloseDuration, () =>
    {
      if (isOpen && !IsConfirmOpen)
        Close();
    });
  }

  // --------------------------------------------------------
  // CONFIRM
  // --------------------------------------------------------

  private void OnClickHall(int index)
  {
    if (isSwitching || index < 0 || index >= buttonHallIds.Length || string.IsNullOrEmpty(buttonHallIds[index]))
      return;

    // The panel stays up while the popup is open.
    autoCloseTween?.Kill();
    pendingHall = buttonHallIds[index];

    if (!confirmPopup)
    {
      GameLog.Warn(LogCategory.Socket, "Hall switcher: no confirm popup assigned, switching directly");
      OnClickConfirmYes();
      return;
    }

    if (confirmMessageText)
      confirmMessageText.text = $"Do you want to switch to {HallName(pendingHall)}?";
    confirmPopup.SetActive(true);
  }

  private void OnClickConfirmNo()
  {
    pendingHall = null;
    if (confirmPopup) confirmPopup.SetActive(false);
    if (isOpen)
      StartAutoClose();
  }

  private void OnClickConfirmYes()
  {
    string hall = pendingHall;
    pendingHall = null;
    if (confirmPopup) confirmPopup.SetActive(false);
    if (string.IsNullOrEmpty(hall))
      return;

    Close();
    BeginSwitch(hall);
  }

  // --------------------------------------------------------
  // SWITCH
  // --------------------------------------------------------

  private void BeginSwitch(string hall)
  {
    var socket = SocketIOManager.Instance;
    if (socket == null || !socket.IsConnected)
    {
      GameLog.Warn(LogCategory.Socket, $"Can't switch to hall '{hall}': not connected");
      return;
    }

    GameLog.Info(LogCategory.Socket, $"Switching hall {currentHall} -> {hall}");
    isSwitching = true;
    switchMinElapsed = false;
    switchAcked = false;
    switchInitReceived = false;

    GunManager.Instance?.SetBulletFiring(false);
    if (loadingPanel)
      loadingPanel.SetActive(true);

    switchMinTween?.Kill();
    switchMinTween = DOVirtual.DelayedCall(minLoadingDuration, () =>
    {
      switchMinElapsed = true;
      TryFinishSwitch();
    });

    switchTimeoutTween?.Kill();
    switchTimeoutTween = DOVirtual.DelayedCall(switchTimeout, () =>
    {
      if (isSwitching)
        EndSwitch($"Hall switch to '{hall}' timed out (ack {switchAcked}, init {switchInitReceived})", false);
    });

    socket.SendJoinLevel(hall, ok =>
    {
      if (!isSwitching)
        return;
      if (!ok)
      {
        EndSwitch($"Hall switch to '{hall}' failed", false);
        return;
      }
      switchAcked = true;
      TryFinishSwitch();
    });
  }

  private void TryFinishSwitch()
  {
    if (isSwitching && switchMinElapsed && switchAcked && switchInitReceived)
      EndSwitch($"Switched to hall '{currentHall}'", true);
  }

  private void EndSwitch(string message, bool ok)
  {
    isSwitching = false;
    switchMinTween?.Kill();
    switchTimeoutTween?.Kill();

    if (ok) GameLog.Info(LogCategory.Socket, message);
    else GameLog.Error(LogCategory.Socket, message);

    if (loadingPanel)
      loadingPanel.SetActive(false);
    Open();
  }
}
