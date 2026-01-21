using System.Collections;
using UnityEngine;

internal class ImmortalFish : BaseFish
{
  private Coroutine speedPulseRoutine;
  private Coroutine winAnimSpeedRoutine;
  [SerializeField] private ImageAnimation bubbleSpreadAnim;
  [SerializeField] private float winAnimSpeedMultiplier = 3f;
  [SerializeField] private float winAnimSpeedDuration = 1.2f;
  internal bool BucketAnimPlaying { get; private set; }

  internal void SetBucketAnimPlaying(bool isPlaying)
  {
    BucketAnimPlaying = isPlaying;
  }
  internal override void Initialize(FishData data)
  {
    base.Initialize(data);
    SetupFallbackMovement();
    if (data.variant == "immo_squid_fish")
    {
      speedPulseRoutine = StartCoroutine(OctopusSpeedPulse());
      SetSpeedMultiplier(1.6f);
    }
  }

private IEnumerator OctopusSpeedPulse()
{
  while (true)
  {
    yield return new WaitForSecondsRealtime(0.8f);
    SetSpeedMultiplier(0.6f);   // glide

    yield return new WaitForSecondsRealtime(0.7f);
    SetSpeedMultiplier(1.8f);   // push
    PlayBubbleSpread();
  }
}

  private void PlayBubbleSpread()
  {
    bubbleSpreadAnim.OnAnimationComplete = null;
    bubbleSpreadAnim.OnAnimationComplete = () =>
    {
      bubbleSpreadAnim.gameObject.SetActive(false);
    };

    bubbleSpreadAnim.gameObject.SetActive(true);
    bubbleSpreadAnim.StopAnimation();
    bubbleSpreadAnim.StartAnimation();
  }

  internal void TriggerWinAnimSpeedBoost()
  {
    if (winAnimSpeedRoutine != null)
      StopCoroutine(winAnimSpeedRoutine);
    winAnimSpeedRoutine = StartCoroutine(WinAnimSpeedBoost());
  }

  private IEnumerator WinAnimSpeedBoost()
  {
    SetAnimationSpeedMultiplier(winAnimSpeedMultiplier);
    yield return new WaitForSecondsRealtime(winAnimSpeedDuration);
    SetAnimationSpeedMultiplier(1f);
    winAnimSpeedRoutine = null;
  }


  internal override void ResetFish()
  {
    base.ResetFish();
    if(speedPulseRoutine != null)
    {
      StopCoroutine(speedPulseRoutine);
    }
    BucketAnimPlaying = false;
    bubbleSpreadAnim.OnAnimationComplete = null;
    bubbleSpreadAnim.StopAnimation();
    bubbleSpreadAnim.gameObject.SetActive(false);
    if (winAnimSpeedRoutine != null)
    {
      StopCoroutine(winAnimSpeedRoutine);
      winAnimSpeedRoutine = null;
    }
    SetAnimationSpeedMultiplier(1f);
  }
}
