using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using System.Linq;

public class UIManager : MonoBehaviour
{
  public enum GunType
  {
    Simple,
    Laser,
    Torpedo
  }

  internal static UIManager Instance;

  [Header("Gun Panel")]
  [SerializeField] private TMP_Text BalanceText;
  [SerializeField] private TMP_Text TotalBetText;
  [SerializeField] private Button PlusBetButton;
  [SerializeField] private Button MinusBetButton;
  internal float currentBet;
  internal float currentBalance;

  [Header("Refund Text")]
  [SerializeField] private Transform refundTextAnchor;
  [SerializeField] private float refundTextOffsetX;
  [SerializeField] private float refundTextOffsetY;

  [Header("Win Animation")]
  [SerializeField] private RainbowAnimationPool rainbowAnimationPool;

  [Header("LEFT PANEL")]
  [SerializeField] private Button LeftPanelopenbtn;
  [SerializeField] private Button LeftPanelBGCloseButton;
  [SerializeField] private RectTransform Leftpanel;
  [SerializeField] private Button InfoBtn;
  [SerializeField] private Button SoundOnBtn;
  [SerializeField] private Button SoundOffBtn;
  [SerializeField] private Button RoosterBtn;

  [Header("Top PANEL")]
  [SerializeField] private Button TopBtn;
  [SerializeField] private RectTransform ExpandedTopPanel;

  [Header("Right PANEL")]
  [SerializeField] private Button TargetLockBttn;
  [SerializeField] private GameObject TargetLockFGAnim;
  [SerializeField] private Button TorpedoBttn;
  [SerializeField] private GameObject TorpedoFGAnim;
  [SerializeField] private GameObject TorpedoText;
  [SerializeField] private GameObject TorpedoBulletValueGO;
  [SerializeField] private TMP_Text TorpedoBulletValueText;
  [SerializeField] private Button AutoAimBtn;
  [SerializeField] private Button AutoSelectBtn;

  [Header("Freeze (art pending, all optional)")]
  [SerializeField] private Button freezeButton;
  [SerializeField] private TMP_Text freezeCostText;
  [Tooltip("Black Filled (Radial 360) image over the button: the freeze timer. Off by default; fillAmount 1 -> 0 over the freeze.")]
  [SerializeField] private Image freezeOverlay;
  [Tooltip("Placeholder for the screen freeze effect (art pending): full-screen overlay above the fish, on while the room is frozen.")]
  [SerializeField] private GameObject screenFreezeOverlay;
  private Coroutine freezeCountdown;
  private double ownFreezeEndMs; // the button stays off until our own freeze ends
  private bool freezeRequestPending;
  private bool OwnFreezeActive => ServerClock.NowMs < ownFreezeEndMs;

  [Header("Popup PANEL")]

  [SerializeField] private Button PopupPanelBGButton;
  [SerializeField] private GameObject RoosterObject;
  [SerializeField] private GameObject InfoObject;
  [SerializeField] private GameObject QuitPopupObject;
  [SerializeField] private GameObject LowBalancePopupObject;
  [SerializeField] private GameObject ReconnectionPopupObject;
  [SerializeField] private GameObject DisconnectPopupObject;

  [Header("Low Balance Panel")]
  [SerializeField] private Button LowBalanceCloseBtn;

  [Header("Disconnect Panel")]
  [SerializeField] private Button DisconnectPopupCloseBtn;

  [Header("Quit Panel")]
  [SerializeField] private Button QuitBtn;
  [SerializeField] private Button QuitYesBtn;
  [SerializeField] private Button QuitNoBtn;

  [Header("Help Panel")]
  [SerializeField] private Button CloseHelp;

  [SerializeField] private Button FeatureBtn;
  [SerializeField] private GameObject FeatureHighlight;
  [SerializeField] private GameObject FeaturePanel;

  [SerializeField] private Button PaytableBtn;
  [SerializeField] private GameObject PaytableHighlight;
  [SerializeField] private GameObject PaytablePanel;

  [SerializeField] private Button UIBtn;
  [SerializeField] private GameObject UIHighlight;
  [SerializeField] private GameObject UIPanel;

  [Header("Info Panel")]
  [SerializeField] private Button HNavPrev;
  [SerializeField] private Button HNavNext;
  [SerializeField] private TMP_Text pageCount;
  [SerializeField] private List<GameObject> InfoPages;

  [Header("Paytable Panel")]
  [SerializeField] private Button INavPrev;
  [SerializeField] private Button INavNext;
  [SerializeField] private TMP_Text IpageCount;
  [SerializeField] private List<GameObject> PaytablePages;
  [SerializeField] private List<TMP_Text> NormalFishesMultTexts;
  [SerializeField] private List<TMP_Text> GoldenFishMultTexts;
  [SerializeField] private List<TMP_Text> SpecialFishesMultTexts;
  [SerializeField] private List<TMP_Text> SpecialFishesMultTexts2;
  [SerializeField] private List<TMP_Text> EffectFishsesMultTexts;
  [SerializeField] private List<TMP_Text> ImmortalFishesMultTexts;
  [SerializeField] private TMP_Text JackpotFishesMultText;
  [SerializeField] private TMP_Text JackpotDragonMultText;

  [Header("Close Panel Btn")]
  [SerializeField] private Button CloseRooster;

  [Header("Lifecycle")]
  [SerializeField] private JSFunctCalls jsFunctCalls;
  [SerializeField] private AudioController audioController;

  private int infoPageIndex = 0;
  private int paytablePageIndex = 0;
  private float slideDistance = 1080f;
  private Dictionary<Button, (GameObject highlight, GameObject panel)> helpMap;
  private bool isLeftPanelOpen = false;
  private bool isTopPanelOpen = false;
  private bool isTargetLock = false;
  private bool isTorpedoGun = false;
  private Tween balanceTween;
  private double serverBalance;
  internal GunType activeGun = GunType.Simple;
  internal int BetCounter = 0;
  internal bool IsTargetLockEnabled => isTargetLock;
  private bool UserExited = false;
  private bool isSound = true;

  void Awake()
  {
    Instance = this;
    if (jsFunctCalls != null)
      jsFunctCalls.RegisterVisibilityListener(gameObject.name);
  }

  // Called from JS via SendMessage('UIManager', 'OnFocusChanged', '1'|'0'). Must stay public.
  public void OnFocusChanged(string value)
  {
    bool focused = value == "1";
    GameLog.Info(LogCategory.Lifecycle, "Focus changed: " + (focused ? "focused" : "blurred"));
    audioController?.SetMuteAll(focused ? !isSound : true);
    SocketIOManager.Instance?.HandleFocusChange(focused);
  }

  void Start()
  {
    if (freezeButton)
    {
      freezeButton.onClick.RemoveAllListeners();
      freezeButton.onClick.AddListener(OnClickFreeze);
    }
    if (freezeOverlay) freezeOverlay.gameObject.SetActive(false);
    if (screenFreezeOverlay) screenFreezeOverlay.SetActive(false);

    if (LowBalanceCloseBtn)
    {
      LowBalanceCloseBtn.onClick.RemoveAllListeners();
      LowBalanceCloseBtn.onClick.AddListener(OnClickClosePopup);
    }

    if (DisconnectPopupCloseBtn)
    {
      DisconnectPopupCloseBtn.onClick.RemoveAllListeners();
      DisconnectPopupCloseBtn.onClick.AddListener(OnClickQuitYes);
    }
    // --- Quit Panel ---
    if (QuitBtn)
    {
      QuitBtn.onClick.RemoveAllListeners();
      QuitBtn.onClick.AddListener(OnClickQuitPopup);
    }
    if (QuitYesBtn)
    {
      QuitYesBtn.onClick.RemoveAllListeners();
      QuitYesBtn.onClick.AddListener(OnClickQuitYes);
    }

    if (QuitNoBtn)
    {
      QuitNoBtn.onClick.RemoveAllListeners();
      QuitNoBtn.onClick.AddListener(OnClickQuitNo);
    }

    if (InfoBtn) InfoBtn.onClick.RemoveAllListeners();
    if (InfoBtn) InfoBtn.onClick.AddListener(OnClickInfo);

    if (LeftPanelopenbtn) LeftPanelopenbtn.onClick.RemoveAllListeners();
    if (LeftPanelopenbtn) LeftPanelopenbtn.onClick.AddListener(OnClickOpenLeftpanel);

    if (TopBtn)
    {
      TopBtn.onClick.RemoveAllListeners();
      TopBtn.onClick.AddListener(OnClickOpenTopPanel);
    }

    if (PopupPanelBGButton)
    {
      PopupPanelBGButton.onClick.RemoveAllListeners();
      PopupPanelBGButton.onClick.AddListener(OnClickClosePopup);
    }

    if (LeftPanelBGCloseButton)
    {
      LeftPanelBGCloseButton.onClick.RemoveAllListeners();
      LeftPanelBGCloseButton.onClick.AddListener(OnClickOpenLeftpanel);
    }

    if (CloseHelp)
    {
      CloseHelp.onClick.RemoveAllListeners();
      CloseHelp.onClick.AddListener(OnClickClosePopup);
    }
    if (CloseRooster)
    {
      CloseRooster.onClick.RemoveAllListeners();
      CloseRooster.onClick.AddListener(OnClickClosePopup);
    }
    // --- Left Panel Inner Buttons ---
    if (InfoBtn)
    {
      InfoBtn.onClick.RemoveAllListeners();
      InfoBtn.onClick.AddListener(OnClickInfo);
    }

    if (RoosterBtn)
    {
      RoosterBtn.onClick.RemoveAllListeners();
      RoosterBtn.onClick.AddListener(OnClickRooster);
    }

    if (SoundOnBtn)
    {
      SoundOnBtn.onClick.RemoveAllListeners();
      SoundOnBtn.onClick.AddListener(OnClickSoundOn);
    }

    if (SoundOffBtn)
    {
      SoundOffBtn.onClick.RemoveAllListeners();
      SoundOffBtn.onClick.AddListener(OnClickSoundOff);
    }

    // --- Help panel ---
    helpMap = new Dictionary<Button, (GameObject, GameObject)>()
        {
            { FeatureBtn, (FeatureHighlight, FeaturePanel) },
            { PaytableBtn, (PaytableHighlight, PaytablePanel) },
            { UIBtn, (UIHighlight, UIPanel) },
        };

    foreach (var kvp in helpMap)
    {
      Button btn = kvp.Key;
      btn.onClick.RemoveAllListeners();
      btn.onClick.AddListener(() => OnHelpTabClicked(btn));
    }

    OnHelpTabClicked(FeatureBtn);

    // --- info panel ---
    // Info navigation
    if (HNavNext) HNavNext.onClick.AddListener(NextInfoPage);
    if (HNavPrev) HNavPrev.onClick.AddListener(PrevInfoPage);
    ShowInfoPage();

    // Paytable navigation
    if (INavNext) INavNext.onClick.AddListener(NextPaytablePage);
    if (INavPrev) INavPrev.onClick.AddListener(PrevPaytablePage);
    ShowPaytablePage();

    // --- Initial states ---
    if (Leftpanel)
      Leftpanel.anchoredPosition = new Vector2(-280f, Leftpanel.anchoredPosition.y);

    if (ExpandedTopPanel)
      ExpandedTopPanel.anchoredPosition = new Vector2(ExpandedTopPanel.anchoredPosition.x, 200f);


    if (TargetLockBttn)
    {
      TargetLockBttn.onClick.AddListener(() => OnClickGunSwitch(0));
    }

    if (TorpedoBttn)
    {
      TorpedoBttn.onClick.AddListener(() => OnClickGunSwitch(1));
    }

    if (PlusBetButton)
    {
      PlusBetButton.onClick.RemoveAllListeners();
      PlusBetButton.onClick.AddListener(() => ChangeBet(true));
    }

    if (MinusBetButton)
    {
      MinusBetButton.onClick.RemoveAllListeners();
      MinusBetButton.onClick.AddListener(() => ChangeBet(false));
    }
  }

  // The local player's seat rig owns the gun panel; rebind to it on every init.
  internal void BindGunPanel(SeatRig rig)
  {
    if (rig == null)
      return;

    if (rig.plusBetButton) PlusBetButton = rig.plusBetButton;
    if (rig.minusBetButton) MinusBetButton = rig.minusBetButton;
    if (rig.betText) TotalBetText = rig.betText;
    if (rig.balanceText) BalanceText = rig.balanceText;

    if (PlusBetButton)
    {
      PlusBetButton.onClick.RemoveAllListeners();
      PlusBetButton.onClick.AddListener(() => ChangeBet(true));
    }

    if (MinusBetButton)
    {
      MinusBetButton.onClick.RemoveAllListeners();
      MinusBetButton.onClick.AddListener(() => ChangeBet(false));
    }

    SetBetText();
    RefreshBetButtons();
    SnapBalanceDisplay();
  }

  internal void HandeGameInit()
  {
    // Each hall has its own bet list, so an index from the last init may not exist.
    OnBetChanged(0);
    if (freezeCostText && SocketIOManager.Instance != null)
      freezeCostText.text = SocketIOManager.Instance.FreezeCost.ToString("0.##");
    // Paused: old-backend paytable multipliers. ML-FG init doesn't send them yet.
    // if (SocketIOManager.Instance?.initFishes != null)
    //   UpdatePaytableMultipliers(SocketIOManager.Instance.initFishes);
  }

  // Bets don't loop: + stops at the last bet and - at the first.
  void ChangeBet(bool IncDec)
  {
    int count = SocketIOManager.Instance?.bets?.Count ?? 0;
    int next = Mathf.Clamp(BetCounter + (IncDec ? 1 : -1), 0, Mathf.Max(0, count - 1));
    if (next == BetCounter)
      return;
    OnBetChanged(next);
  }

  // + is off on the last bet, - on the first.
  private void RefreshBetButtons()
  {
    int count = SocketIOManager.Instance?.bets?.Count ?? 0;
    if (PlusBetButton) PlusBetButton.interactable = BetCounter < count - 1;
    if (MinusBetButton) MinusBetButton.interactable = BetCounter > 0;
  }

  // Every change of the local bet goes through here, including the reset on each init.
  private void OnBetChanged(int betIndex)
  {
    BetCounter = betIndex;
    SetBetText();
    SetTorpedoBulletValue();
    RefreshBetButtons();
    GunManager.Instance?.ApplyLocalBet(betIndex);
  }

  void SetBetText()
  {
    currentBet = SocketIOManager.Instance?.bets[BetCounter] ?? 0;
    if (TotalBetText) TotalBetText.text = currentBet.ToString("N2");
  }

  void SetTorpedoBulletValue()
  {
    float torpedoBulletValue = SocketIOManager.Instance?.bets[BetCounter] * SocketIOManager.Instance?.GunCosts[1] ?? 0;
    TorpedoBulletValueText.text = torpedoBulletValue.ToString();
  }

  internal void UpdateBalance(float bal)
  {
    if (currentBalance != bal)
    {
      float UiBalance = currentBalance;
      balanceTween?.Kill();
      balanceTween = DOTween.To(() => UiBalance, (val) => UiBalance = val, bal, 0.1f).OnUpdate(() =>
      {
        SetBalanceText(UiBalance);
      });
      currentBalance = bal;
    }
  }

  void SetBalanceText(float val)
  {
    if (BalanceText) BalanceText.text = val.ToString("N2");
  }

  // --------------------------------------------------------
  // BALANCE: always the last value the server sent, never calculated here.
  // --------------------------------------------------------

  // game:init
  internal void ResetBalance(double balance)
  {
    GameLog.Info(LogCategory.Balance, $"Init balance {balance:N2}");
    serverBalance = balance;
    SnapBalanceDisplay();
  }

  // FIRE ack, SHOOT ack or own shot_result.
  internal void ApplyServerBalance(double balance)
  {
    GameLog.Info(LogCategory.Balance, $"Server balance {balance:N2}");
    serverBalance = balance;
    UpdateBalance((float)serverBalance);
  }

  // balance:sync: an external correction, so snap (no tween) and re-check the fire gate.
  internal void SyncBalance(double balance)
  {
    GameLog.Info(LogCategory.Balance, $"balance:sync {balance:N2}");
    serverBalance = balance;
    SnapBalanceDisplay();
    if (serverBalance < GetGunCost())
      ShowLowBalancePopup();
  }

  // Fire gate only; the server makes the final call and sends the new balance in the FIRE ack.
  internal bool CanAffordShot()
  {
    if (serverBalance >= GetGunCost())
      return true;

    GameLog.Info(LogCategory.Balance, $"Can't afford {GetGunCost():N2} (balance {serverBalance:N2})");
    ShowLowBalancePopup();
    return false;
  }

  internal void OnFireRejected(string message)
  {
    if (!string.IsNullOrEmpty(message) && message.Contains("Insufficient balance"))
      ShowLowBalancePopup();
  }

  // --------------------------------------------------------
  // ROOM FREEZE
  // --------------------------------------------------------

  private void OnClickFreeze()
  {
    if (freezeRequestPending || OwnFreezeActive || SocketIOManager.Instance == null)
      return;

    float cost = SocketIOManager.Instance.FreezeCost;
    if (serverBalance < cost)
    {
      GameLog.Info(LogCategory.Balance, $"Can't afford freeze {cost:N2} (balance {serverBalance:N2})");
      ShowLowBalancePopup();
      return;
    }

    freezeRequestPending = true;
    if (freezeButton) freezeButton.interactable = false;
    SocketIOManager.Instance.SendFreeze(ok =>
    {
      freezeRequestPending = false;
      // On success the button timer (OnRoomFreezeStarted) keeps it off until our freeze ends.
      if (freezeButton) freezeButton.interactable = !OwnFreezeActive;
    });
  }

  internal void OnFreezeRejected(string message)
  {
    if (!string.IsNullOrEmpty(message) && message.Contains("Insufficient balance"))
      ShowLowBalancePopup();
    // TODO: show other rejection messages once the backend's list is known.
  }

  // Each new freeze in the room (ack, game:room_frozen, init / JOIN_LEVEL ack), anyone's.
  // The screen overlay is room-wide; the button timer only runs for our own freeze, so the
  // button stays usable during an opponent's (double freeze).
  internal void OnRoomFreezeStarted(double startMs, double endMs, bool own)
  {
    GameLog.Info(LogCategory.UI, $"Freeze started until {endMs:F0}, own {own}");
    // TODO (art pending): replace the black screen overlay with the real freeze effect and sound,
    // and show "frozen by {username}" for opponents.
    if (screenFreezeOverlay) screenFreezeOverlay.SetActive(true);

    if (!own)
      return;

    StopFreezeTimer();
    ownFreezeEndMs = endMs;
    if (freezeButton) freezeButton.interactable = false;
    freezeCountdown = StartCoroutine(FreezeTimer(endMs, endMs - startMs));
  }

  // The room unfroze or was re-initialised.
  internal void OnRoomFreezeEnded()
  {
    GameLog.Info(LogCategory.UI, "Freeze ended");
    if (screenFreezeOverlay) screenFreezeOverlay.SetActive(false);
    StopFreezeTimer();
    ownFreezeEndMs = 0;
    if (freezeButton) freezeButton.interactable = !freezeRequestPending;
  }

  private void StopFreezeTimer()
  {
    if (freezeCountdown != null)
    {
      StopCoroutine(freezeCountdown);
      freezeCountdown = null;
    }
    if (freezeOverlay) freezeOverlay.gameObject.SetActive(false);
  }

  // Our own freeze's button timer: the black radial overlay goes 1 -> 0. Driven by the server
  // clock, not a tween, so a rejoin mid-freeze starts part-revealed and a hidden tab catches up.
  // The fill direction (clockwise flag) is set on the Image.
  private IEnumerator FreezeTimer(double endMs, double durationMs)
  {
    if (freezeOverlay)
    {
      freezeOverlay.fillAmount = 1f;
      freezeOverlay.gameObject.SetActive(true);
    }

    while (ServerClock.NowMs < endMs)
    {
      double leftMs = endMs - ServerClock.NowMs;
      if (freezeOverlay)
        freezeOverlay.fillAmount = durationMs > 0 ? (float)Math.Min(1.0, leftMs / durationMs) : 0f;
      yield return null;
    }

    if (freezeOverlay) freezeOverlay.gameObject.SetActive(false);
    if (freezeButton) freezeButton.interactable = !freezeRequestPending;
    freezeCountdown = null;
  }

  private void SnapBalanceDisplay()
  {
    balanceTween?.Kill();
    currentBalance = (float)serverBalance;
    SetBalanceText(currentBalance);
  }

  internal void PlayRefundText(float amount)
  {
    if (amount <= 0f)
      return;
    if (refundTextAnchor == null)
      return;

    var refund = RefundTextPool.Instance.GetFromPool();
    if (refund == null)
      return;

    float offsetX = UnityEngine.Random.Range(0f, refundTextOffsetX);
    float offsetY = UnityEngine.Random.Range(0f, refundTextOffsetY);
    Vector3 pos = refundTextAnchor.position +
                  new Vector3(offsetX, offsetY, 0f);
    refund.Play(pos, amount);
  }

  internal void PlayCoinBlastForFish(BaseFish fish)
  {
    if (fish == null || fish.data == null)
      return;

    if (fish.data.fishType == FishType.Normal ||
        fish.data.fishType == FishType.Immortal)
      return;

    var coinAnimation = CoinBlastAnimPool.Instance.GetFromPool();
    coinAnimation.transform.SetPositionAndRotation(
      fish.ColliderMidPoint,
      Quaternion.identity
    );
    float scale = FishManager.Instance != null
      ? FishManager.Instance.GetCoinBlastScale(fish.data.fishType)
      : 1f;
    if (scale <= 0f)
      scale = 1f;
    coinAnimation.transform.localScale = Vector3.one * scale;
  }

  internal void PlayRainbowWinAnimation(BaseFish fish, FishData fishData, float winAmount)
  {
    if (fish == null || fishData == null || winAmount <= 0)
      return;
    if (fishData.fishType == FishType.Normal)
      return;

    var pool = rainbowAnimationPool != null ? rainbowAnimationPool : RainbowAnimationPool.Instance;
    if (pool == null)
    {
      GameLog.Error(LogCategory.UI, "RainbowAnimationPool not found");
      return;
    }

    var anim = pool.GetFromPool();
    if (anim == null)
      return;

    anim.Play(fish, fishData, winAmount);
  }

  void OnClickGunSwitch(int index) //0: target lock 1: torpedo
  {
    GunType previousGun = activeGun;
    bool wasTargetLock = isTargetLock;
    bool clearedLock = false;

    switch (index)
    {
      case 0:
        isTargetLock = !isTargetLock;
        if (isTargetLock)
          TargetLockFGAnim.SetActive(true);
        else
          TargetLockFGAnim.SetActive(false);
        if (wasTargetLock && !isTargetLock)
        {
          ClearCurrentGunLock();
          clearedLock = true;
        }
        UpdateActiveGun();
        break;
      case 1:
        isTorpedoGun = !isTorpedoGun;
        if (isTorpedoGun)
        {
          TorpedoFGAnim.SetActive(true);
          TorpedoText.SetActive(false);
          TorpedoBulletValueGO.SetActive(true);
        }
        else
        {
          TorpedoFGAnim.SetActive(false);
          TorpedoText.SetActive(true);
          TorpedoBulletValueGO.SetActive(false);
        }
        UpdateActiveGun();
        break;
    }

    if (previousGun != activeGun && !clearedLock)
      ClearCurrentGunLock();

    if (!isTargetLock && !isTorpedoGun)
    {
      GunManager.Instance.SwitchGun<SimpleGun>();
    }
    else if (isTorpedoGun && !isTargetLock)
    {
      GunManager.Instance.SwitchGun<TorpedoGun>();
    }
    else if (!isTorpedoGun && isTargetLock)
    {
      GunManager.Instance.SwitchGun<LazerGun>();
    }
    else if (isTorpedoGun && isTargetLock)
    {
      GunManager.Instance.SwitchGun<TorpedoGun>();
    }
  }

  private void ClearCurrentGunLock()
  {
    if (GunManager.Instance == null || GunManager.Instance.currentGun == null)
      return;

    if (GunManager.Instance.currentGun is TorpedoGun torpedoGun)
      torpedoGun.DisableTargetLock();
    else if (GunManager.Instance.currentGun is LazerGun lazerGun)
      lazerGun.DisableTargetLock();
  }

  // Torpedo and laser fire gate; the server charges the shot in the FIRE ack.
  internal bool OnGunFired()
  {
    return CanAffordShot();
  }

  internal float GetGunCost()
  {
    float bet = currentBet;

    return activeGun switch
    {
      GunType.Simple => bet * SocketIOManager.Instance.GunCosts[0],
      GunType.Torpedo => bet * SocketIOManager.Instance.GunCosts[1],
      GunType.Laser => bet * SocketIOManager.Instance.GunCosts[2],
      _ => 0f
    };
  }

  void UpdateActiveGun()
  {
    if (!isTargetLock && !isTorpedoGun)
      activeGun = GunType.Simple;
    else if (isTorpedoGun)
      activeGun = GunType.Torpedo;
    else
      activeGun = GunType.Laser;

    UpdateTorpedoFishVisuals();
  }

  void OnClickClosePopup()
  {
    if (!DisconnectPopupObject.activeInHierarchy && !ReconnectionPopupObject.activeInHierarchy)
      PopupPanelBGButton.gameObject.SetActive(false);
    LowBalancePopupObject.SetActive(false);
    QuitPopupObject.SetActive(false);
    InfoObject.SetActive(false);
    RoosterObject.SetActive(false);
  }
  #region LEftpanel

  void OnClickOpenLeftpanel()
  {
    if (!isLeftPanelOpen)
    {
      Leftpanel.DOAnchorPosX(0f, 0.4f).SetEase(Ease.OutCubic);
      isLeftPanelOpen = true;
      LeftPanelBGCloseButton.gameObject.SetActive(true);
    }
    else
    {
      Leftpanel.DOAnchorPosX(-280f, 0.4f).SetEase(Ease.InCubic);
      isLeftPanelOpen = false;
      LeftPanelBGCloseButton.gameObject.SetActive(false);
    }
  }

  void ShowLowBalancePopup()
  {
    if (!PopupPanelBGButton || !LowBalancePopupObject) return;
    PopupPanelBGButton.gameObject.SetActive(true);
    LowBalancePopupObject.SetActive(true);
  }

  void OnClickQuitPopup()
  {
    if (!PopupPanelBGButton || !QuitPopupObject) return;
    OnClickOpenLeftpanel();
    PopupPanelBGButton.gameObject.SetActive(true);
    QuitPopupObject.SetActive(true);
  }

  void OnClickQuitYes()
  {
    if (UserExited)
      return;
    UserExited = true;
    SocketIOManager.Instance.CloseGame();
  }

  void OnClickQuitNo()
  {
    OnClickClosePopup();
  }

  void OnClickInfo()
  {
    if (!PopupPanelBGButton || !InfoObject) return;
    OnClickOpenLeftpanel();
    PopupPanelBGButton.gameObject.SetActive(true);
    InfoObject.SetActive(true);
  }

  void OnClickRooster()
  {
    if (!PopupPanelBGButton || !RoosterObject) return;
    OnClickOpenLeftpanel();
    PopupPanelBGButton.gameObject.SetActive(true);
    RoosterObject.SetActive(true);
  }

  void OnClickSoundOn()
  {
    // Turn sound OFF mode
    SetSound(false);
  }

  void OnClickSoundOff()
  {
    // Turn sound ON mode
    SetSound(true);
  }

  private void SetSound(bool soundOn)
  {
    isSound = soundOn;
    GameLog.Info(LogCategory.UI, isSound ? "🔊 Sound on" : "🔇 Sound off");
    audioController?.SetUserMute(!isSound);
    if (SoundOnBtn) SoundOnBtn.gameObject.SetActive(isSound);
    if (SoundOffBtn) SoundOffBtn.gameObject.SetActive(!isSound);
  }
  #endregion

  #region Top Panel
  void OnClickOpenTopPanel()
  {
    if (!ExpandedTopPanel) return;

    if (!isTopPanelOpen)
      ExpandedTopPanel.DOAnchorPosY(0f, 0.4f).SetEase(Ease.OutCubic);
    else
      ExpandedTopPanel.DOAnchorPosY(200f, 0.4f).SetEase(Ease.InCubic);

    isTopPanelOpen = !isTopPanelOpen;
  }
  #endregion

  // Hall Selection panel: moved to HallSwitcherManager.

  internal void DisconnectionPopup()
  {
    if (UserExited)
      return;

    OnClickClosePopup();
    PopupPanelBGButton.gameObject.SetActive(true);
    DisconnectPopupObject.SetActive(true);
  }

  internal void ReconnectionPopup()
  {
    OnClickClosePopup();
    PopupPanelBGButton.gameObject.SetActive(true);
    ReconnectionPopupObject.SetActive(true);
  }

  internal void CheckAndClosePopups()
  {
    if (PopupPanelBGButton.gameObject.activeInHierarchy == false)
      return;
    if (DisconnectPopupObject.activeInHierarchy)
    {
      DisconnectPopupObject.SetActive(false);
      PopupPanelBGButton.gameObject.SetActive(false);
    }
    else if (ReconnectionPopupObject.activeInHierarchy)
    {
      ReconnectionPopupObject.SetActive(false);
      PopupPanelBGButton.gameObject.SetActive(false);
    }
  }
  #region Infopanel


  void OnHelpTabClicked(Button clickedButton)
  {
    foreach (var kvp in helpMap)
    {
      bool isActive = kvp.Key == clickedButton;

      kvp.Value.highlight.SetActive(isActive);
      kvp.Value.panel.SetActive(isActive);
    }
  }
  void ShowPage(List<GameObject> pages, int index, TMP_Text counter)
  {
    if (pages == null || pages.Count == 0)
    {
      if (counter) counter.text = "0 / 0";
      return;
    }

    for (int i = 0; i < pages.Count; i++)
      pages[i].SetActive(i == index);

    if (counter)
      counter.text = $"{index + 1} / {pages.Count}";
  }

  int SlidePage(List<GameObject> pages, int index, TMP_Text counter, int direction)
  {
    if (pages == null || pages.Count == 0) return index;

    int newIndex = index + direction;
    if (newIndex < 0 || newIndex >= pages.Count)
      return index; // stay the same if out of range

    RectTransform current = pages[index].GetComponent<RectTransform>();
    RectTransform next = pages[newIndex].GetComponent<RectTransform>();

    float outPos = direction > 0 ? -slideDistance : slideDistance;
    float inPos = direction > 0 ? slideDistance : -slideDistance;

    next.gameObject.SetActive(true);
    next.anchoredPosition = new Vector2(inPos, 0);

    // Animate slide
    current.DOAnchorPosX(outPos, 0.4f).SetEase(Ease.InOutCubic);
    next.DOAnchorPosX(0f, 0.4f).SetEase(Ease.InOutCubic).OnComplete(() =>
    {
      current.gameObject.SetActive(false);

      if (counter)
        counter.text = $"{newIndex + 1} / {pages.Count}";
    });

    return newIndex;
  }

  #endregion

  // ---------------- INFO PANEL ----------------
  void ShowInfoPage()
  {
    for (int i = 0; i < InfoPages.Count; i++)
      InfoPages[i].SetActive(i == infoPageIndex);

    if (pageCount)
      pageCount.text = $"{infoPageIndex + 1} / {InfoPages.Count}";
  }

  void NextInfoPage()
  {
    if (infoPageIndex < InfoPages.Count - 1)
    {
      infoPageIndex++;
      ShowInfoPage();
    }
  }

  void PrevInfoPage()
  {
    if (infoPageIndex > 0)
    {
      infoPageIndex--;
      ShowInfoPage();
    }
  }

  // ---------------- PAYTABLE PANEL ----------------
  void ShowPaytablePage()
  {
    for (int i = 0; i < PaytablePages.Count; i++)
      PaytablePages[i].SetActive(i == paytablePageIndex);

    if (IpageCount)
      IpageCount.text = $"{paytablePageIndex + 1} / {PaytablePages.Count}";
  }

  void NextPaytablePage()
  {
    if (paytablePageIndex < PaytablePages.Count - 1)
    {
      paytablePageIndex++;
      ShowPaytablePage();
    }
  }

  void PrevPaytablePage()
  {
    if (paytablePageIndex > 0)
    {
      paytablePageIndex--;
      ShowPaytablePage();
    }
  }
  internal void UpdateTorpedoFishVisuals()
  {
    bool torpedoActive = activeGun == GunType.Torpedo;

    foreach (var fish in FishManager.Instance.GetActiveFishes())
    {
      if (fish == null || fish.data == null)
        continue;

      if (fish.PendingVisualDeath)
      {
        fish.SetAlpha(1f);
        continue;
      }

      bool valid = IsValidTorpedoTarget(fish);

      fish.SetAlpha(
        torpedoActive && !valid ? 0.25f : 1f
      );
    }
  }

  internal bool IsValidTorpedoTarget(BaseFish fish)
  {
    if (fish == null || fish.data == null)
      return false;

    // ❌ Variant blacklist (fine-grained control)
    switch (fish.data.variant)
    {
      case "flying_fish":
      case "clownfish":
      case "angelfish":
      case "blowfish":
      case "pufferfish":
      case "sea_turtle":
      case "jellyfish":
      case "hermit_crab":
      case "octopus":
        return false;
    }

    return true;
  }


  void UpdatePaytableMultipliers(List<InitFishGroup> initFishGroup)
  {
    if (initFishGroup == null || initFishGroup.Count <= 0)
    {
      GameLog.Error(LogCategory.Legacy, "Multipliers data is null");
      return;
    }

    var multiplier = initFishGroup[0].multipliers;

    int normalIndex = 0;
    foreach (var text in NormalFishesMultTexts)
    {
      text.text = multiplier.normal[normalIndex].ToString("N0") + " X";
      normalIndex++;
    }

    multiplier = initFishGroup[1].multipliers;

    int goldenIndex = 0;
    foreach (var text in GoldenFishMultTexts)
    {
      text.text = multiplier.golden[goldenIndex].ToString("N0") + " X";
      goldenIndex++;
    }

    multiplier = initFishGroup[2].multipliers;

    int specialIndex = 0;
    foreach (var text in SpecialFishesMultTexts)
    {
      text.text = multiplier.special[specialIndex][0].ToString("N0") + "-" +
                  multiplier.special[specialIndex][1].ToString("N0") + " X\n" +
                  multiplier.specialWin[specialIndex][0].ToString("N0") + "-" +
                  multiplier.specialWin[specialIndex][1].ToString("N0") + " X";
      specialIndex++;
    }

    int special2Index = 0;
    foreach (var text in SpecialFishesMultTexts2)
    {
      text.text = multiplier.special[special2Index][0].ToString("N0") + "-" +
                  multiplier.special[special2Index][1].ToString("N0") + " X";
      special2Index++;
    }

    multiplier = initFishGroup[3].multipliers;

    int effectIndex = 0;
    foreach (var text in EffectFishsesMultTexts)
    {
      text.text = multiplier.effect[effectIndex][0].ToString("N0") + ", " + 
                  multiplier.effect[effectIndex][1].ToString("N0") + " X";
      effectIndex++;
    }

    multiplier = initFishGroup[4].multipliers;

    int immortalIndex = 0;
    foreach (var text in ImmortalFishesMultTexts)
    {
      if (immortalIndex != 2)
      {
        text.text = multiplier.immortal_ocean_king[immortalIndex][0].ToString("N0") + ", " +
                    multiplier.immortal_ocean_king[immortalIndex][1].ToString("N0") + ", " +
                    multiplier.immortal_ocean_king[immortalIndex][2].ToString("N0") + " X";
      }
      else
      {
        text.text = multiplier.immortal_ocean_king[immortalIndex][0].ToString("N0") + "x / " +
                    multiplier.immortal_ocean_king[immortalIndex][1].ToString("N0") + "x, " +
                    multiplier.immortal_ocean_king[immortalIndex][2].ToString("N0") + "x";
      }
      immortalIndex++;
    }

    multiplier = initFishGroup[5].multipliers;

    JackpotFishesMultText.text = multiplier.jackpot_fish[0][0].ToString("N0") + "x, " +
                                 multiplier.jackpot_fish[0][1].ToString("N0") + "x, " +
                                 multiplier.jackpot_fish[0][2].ToString("N0") + "x, Jackpots";

    multiplier = initFishGroup[6].multipliers;

    JackpotDragonMultText.text = multiplier.jackpot_dragon[0][0].ToString("N0") + "x, " +
                                  multiplier.jackpot_dragon[0][1].ToString("N0") + "x, " +
                                  multiplier.jackpot_dragon[0][2].ToString("N0") + "x, Jackpots";

  }
}
