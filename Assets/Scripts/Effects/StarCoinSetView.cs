using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

internal class StarCoinSetView : MonoBehaviour
{
  [SerializeField] private List<RectTransform> coins = new List<RectTransform>();
  [SerializeField] private float scaleUpDuration = 0.2f;
  [SerializeField] private float postScaleHoldDuration = 0.2f;
  [SerializeField] private float coinMoveDuration = 0.6f;
  [SerializeField] private float coinMoveStagger = 0.05f;
  [SerializeField, Range(0.1f, 0.9f)] private float firstTargetMovePercent = 0.7f;

  private readonly List<Vector3> coinInitialScales = new List<Vector3>();
  private readonly List<Vector3> coinInitialPositions = new List<Vector3>();
  private readonly List<Tween> activeTweens = new List<Tween>();
  private int activeMoves;

  private void Awake()
  {
    CacheCoins();
    SetCoinsScale(Vector3.zero);
  }

  private void OnEnable()
  {
    CacheCoins();
    SetCoinsScale(Vector3.zero);
  }

  private void OnDisable()
  {
    KillTweens();
    ResetCoinsToInitial();
    SetCoinsScale(Vector3.zero);
    activeMoves = 0;
  }

  internal void Play(
    Vector3 worldPosition,
    Vector3 firstTargetPosition,
    Vector3 targetPosition,
    System.Action<StarCoinSetView> onComplete
  )
  {
    KillTweens();
    CacheCoins();
    transform.position = worldPosition;
    ResetCoinsToInitial();
    SetCoinsScale(Vector3.zero);

    var scaleSequence = DOTween.Sequence();
    for (int i = 0; i < coins.Count; i++)
    {
      if (coins[i] == null)
        continue;
      Vector3 targetScale = i < coinInitialScales.Count ? coinInitialScales[i] : Vector3.one;
      scaleSequence.Join(
        coins[i].DOScale(targetScale, scaleUpDuration).SetEase(Ease.OutBack)
      );
    }

    if (postScaleHoldDuration > 0f)
      scaleSequence.AppendInterval(postScaleHoldDuration);

    scaleSequence.OnComplete(() => BeginMove(
      firstTargetPosition,
      targetPosition,
      onComplete
    ));
    activeTweens.Add(scaleSequence);
  }

  private void CacheCoins()
  {
    if (coins == null)
      coins = new List<RectTransform>();

    if (coins.Count == 0)
    {
      var anims = GetComponentsInChildren<ImageAnimation>(true);
      for (int i = 0; i < anims.Length; i++)
      {
        if (anims[i] == null)
          continue;
        RectTransform rect = anims[i].GetComponent<RectTransform>();
        if (rect != null)
          coins.Add(rect);
      }
    }

    if (coinInitialScales.Count != coins.Count)
    {
      coinInitialScales.Clear();
      coinInitialPositions.Clear();
      for (int i = 0; i < coins.Count; i++)
      {
        if (coins[i] != null)
        {
          coinInitialScales.Add(coins[i].localScale);
          coinInitialPositions.Add(coins[i].localPosition);
        }
        else
        {
          coinInitialScales.Add(Vector3.one);
          coinInitialPositions.Add(Vector3.zero);
        }
      }
    }
  }

  private void BeginMove(
    Vector3 firstTargetPosition,
    Vector3 targetPosition,
    System.Action<StarCoinSetView> onComplete
  )
  {
    activeMoves = 0;

    for (int i = 0; i < coins.Count; i++)
    {
      RectTransform coin = coins[i];
      if (coin == null)
        continue;

      Vector3 startScale = i < coinInitialScales.Count ? coinInitialScales[i] : Vector3.one;
      Vector3 endPos = targetPosition;
      Vector3 midPos = firstTargetPosition;
      activeMoves++;
      float delay = coinMoveStagger * i;

      float totalDuration = Mathf.Max(0.01f, coinMoveDuration);
      float firstPercent = Mathf.Clamp01(firstTargetMovePercent);
      float firstDuration = Mathf.Max(0.01f, totalDuration * firstPercent);
      float secondDuration = Mathf.Max(0.01f, totalDuration - firstDuration);

      Sequence seq = DOTween.Sequence();
      seq.Append(coin.DOMove(midPos, firstDuration).SetEase(Ease.OutQuad));
      seq.Append(coin.DOMove(endPos, secondDuration).SetEase(Ease.InQuad));
      seq.Join(coin.DOScale(Vector3.zero, secondDuration).SetEase(Ease.InQuad));
      seq.SetDelay(delay);
      seq.OnComplete(() =>
      {
        coin.position = endPos;
        coin.localScale = Vector3.zero;
        activeMoves--;
        if (activeMoves <= 0)
        {
          ResetCoinsToInitial();
          SetCoinsScale(Vector3.zero);
          onComplete?.Invoke(this);
        }
      });

      activeTweens.Add(seq);
    }

    if (activeMoves == 0)
      onComplete?.Invoke(this);
  }


  private void SetCoinsScale(Vector3 scale)
  {
    for (int i = 0; i < coins.Count; i++)
    {
      if (coins[i] != null)
        coins[i].localScale = scale;
    }
  }

  private void ResetCoinsToInitial()
  {
    for (int i = 0; i < coins.Count; i++)
    {
      if (coins[i] == null)
        continue;
      Vector3 pos = i < coinInitialPositions.Count ? coinInitialPositions[i] : Vector3.zero;
      coins[i].localPosition = pos;
    }
  }

  private void KillTweens()
  {
    for (int i = 0; i < activeTweens.Count; i++)
      activeTweens[i]?.Kill();
    activeTweens.Clear();
  }
}
