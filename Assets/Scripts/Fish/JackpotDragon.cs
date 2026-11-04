using System;
using UnityEngine;

internal class JackpotDragon : BaseFish
{
  [Header("Death Animation")]
  [SerializeField] private Sprite[] deathAnimationFrames;
  [SerializeField] private float deathAnimationSpeed = 10f;
  [SerializeField] private Vector2 deathSizeDelta = new Vector2(400f, 400f);
  private float pendingWinAmount;
  private bool playLegacyRewards;


  // Old backend: the hit handler drove the death and its rewards from here.
  internal void TriggerDeathSequence(float winAmount)
  {
    if (State != FishState.Alive)
      return;

    pendingWinAmount = winAmount;
    playLegacyRewards = true;
    OnFishDespawned = null;
    Kill();
  }

  protected override void PlayDeath(Action onComplete)
  {
    if (deathAnimationFrames == null || deathAnimationFrames.Length == 0)
    {
      FinishDeath(onComplete);
      return;
    }

    Rect.sizeDelta = deathSizeDelta;
    imageAnimation.StopAnimation();
    imageAnimation.doLoopAnimation = false;
    imageAnimation.SetAnimationData(deathAnimationFrames, deathAnimationSpeed, false);
    imageAnimation.OnAnimationComplete = () => FinishDeath(onComplete);
    imageAnimation.StartAnimation();
  }

  private void FinishDeath(Action onComplete)
  {
    imageAnimation.OnAnimationComplete = null;

    if (playLegacyRewards)
    {
      UIManager.Instance?.PlayCoinBlastForFish(this);
      UIManager.Instance?.PlayRainbowWinAnimation(this, data, pendingWinAmount);
    }

    base.PlayDeath(onComplete);
  }

  internal override void ResetFish()
  {
    base.ResetFish();
    pendingWinAmount = 0f;
    playLegacyRewards = false;
    if (imageAnimation != null)
      imageAnimation.OnAnimationComplete = null;
  }
}
