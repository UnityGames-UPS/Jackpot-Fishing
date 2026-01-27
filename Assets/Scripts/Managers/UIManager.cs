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

  [Header("Hall Selection PANEL")]
  [SerializeField] private Button HallSelectionBtn;
  [SerializeField] private RectTransform ExpandedRoomPanel;

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

  private int infoPageIndex = 0;
  private int paytablePageIndex = 0;
  private float slideDistance = 1080f;
  private Dictionary<Button, (GameObject highlight, GameObject panel)> helpMap;
  private bool isLeftPanelOpen = false;
  private bool isTopPanelOpen = false;
  private bool isHallSelectionOpen = false;
  private bool isTargetLock = false;
  private bool isTorpedoGun = false;
  private Tween balanceTween;
  internal GunType activeGun = GunType.Simple;
  internal int BetCounter = 0;
  internal bool IsTargetLockEnabled => isTargetLock;
  private bool UserExited = false;

  void Awake()
  {
    Instance = this;
  }

  void Start()
  {
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
    if (HallSelectionBtn)
    {
      HallSelectionBtn.onClick.RemoveAllListeners();
      HallSelectionBtn.onClick.AddListener(OnClickHallSelection);
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
    if (ExpandedRoomPanel)
    {
      ExpandedRoomPanel.localScale = new Vector3(1, 0, 1);
      ExpandedRoomPanel.gameObject.SetActive(false);
    }
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

  internal void HandeGameInit()
  {
    SetBetText();
    SetTorpedoBulletValue();
    UpdatePaytableMultipliers(SocketIOManager.Instance?.initFishes);
  }

  void ChangeBet(bool IncDec)
  {
    if (IncDec)
    {
      BetCounter++;
      if (BetCounter >= SocketIOManager.Instance?.bets.Count)
      {
        BetCounter = 0; // Loop back to the first bet
      }
    }
    else
    {
      BetCounter--;
      if (BetCounter < 0)
      {
        BetCounter = SocketIOManager.Instance?.bets.Count - 1 ?? 0; // Loop to the last bet
      }
    }
    SetBetText();
    SetTorpedoBulletValue();
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
      Debug.LogError("[UIManager] RainbowAnimationPool not found");
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

  internal bool OnGunFired()
  {
    float cost = GetGunCost();

    if (currentBalance < cost)
    {
      ShowLowBalancePopup();
      return false;
    }

    UpdateBalance(currentBalance - cost);
    return true;
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
    if (SoundOnBtn) SoundOnBtn.gameObject.SetActive(false);
    if (SoundOffBtn) SoundOffBtn.gameObject.SetActive(true);
    Debug.Log("🔇 Sound turned OFF");
  }

  void OnClickSoundOff()
  {
    // Turn sound ON mode
    if (SoundOffBtn) SoundOffBtn.gameObject.SetActive(false);
    if (SoundOnBtn) SoundOnBtn.gameObject.SetActive(true);
    Debug.Log("🔊 Sound turned ON");
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

  #region Hall Selection Panel
  void OnClickHallSelection()
  {
    HallSelectionBtn.transform
.DOScaleY(HallSelectionBtn.transform.localScale.y * -1, 0.2f)
.SetEase(Ease.OutBack);

    if (!ExpandedRoomPanel) return;

    if (!isHallSelectionOpen)
    {
      ExpandedRoomPanel.gameObject.SetActive(true);
      ExpandedRoomPanel.localScale = new Vector3(1, 0, 1);

      ExpandedRoomPanel.DOScaleY(1f, 0.4f)
          .SetEase(Ease.OutBack)
          .OnComplete(() => isHallSelectionOpen = true);
    }
    else
    {
      ExpandedRoomPanel.DOScaleY(0f, 0.3f)
          .SetEase(Ease.InBack)
          .OnComplete(() =>
          {
            ExpandedRoomPanel.gameObject.SetActive(false);
            isHallSelectionOpen = false;
          });
    }
  }
  #endregion

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
      case "small_dragon_fish":
      case "orange_fish":
      case "angel_fish":
      case "pinecone_fish":
      case "puffer_fish":
      case "turtle_fish":
      case "jelly_fish":
      case "lion_fish":
      case "babyocto_fish":
        return false;
    }

    return true;
  }


  void UpdatePaytableMultipliers(List<InitFishGroup> initFishGroup)
  {
    if (initFishGroup == null || initFishGroup.Count <= 0)
    {
      Debug.LogError("Multipliers data is null");
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
