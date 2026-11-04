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
    if (data.variant == "king_squid")
      speedPulseRoutine = StartCoroutine(OctopusSpeedPulse());
  }

private IEnumerator OctopusSpeedPulse()
{
  while (true)
  {
    // The glide/push speed changes drove Curvy movement; only the bubble spread remains.
    yield return new WaitForSecondsRealtime(1.5f);
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
