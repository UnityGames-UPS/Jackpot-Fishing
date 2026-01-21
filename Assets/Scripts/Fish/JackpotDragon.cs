using UnityEngine;

internal class JackpotDragon : BaseFish
{
  [Header("Death Animation")]
  [SerializeField] private Sprite[] deathAnimationFrames;
  [SerializeField] private float deathAnimationSpeed = 10f;
  [SerializeField] private Vector2 deathSizeDelta = new Vector2(400f, 400f);
  private bool deathSequencePlaying;
  private float pendingWinAmount;

  internal override void Initialize(FishData data)
  {
    base.Initialize(data);
    SetupFallbackMovement();
  }

  internal void TriggerDeathSequence(float winAmount)
  {
    if (deathSequencePlaying)
      return;

    deathSequencePlaying = true;
    pendingWinAmount = winAmount;
    OnFishDespawned = null;
    MarkPendingDeath();
    StopPathMovement();

    if (deathAnimationFrames == null || deathAnimationFrames.Length == 0)
    {
      HandleDeathAnimationComplete();
      return;
    }

    Rect.sizeDelta = deathSizeDelta;
    imageAnimation.StopAnimation();
    imageAnimation.doLoopAnimation = false;
    imageAnimation.SetAnimationData(deathAnimationFrames, deathAnimationSpeed, false);
    imageAnimation.OnAnimationComplete = HandleDeathAnimationComplete;
    imageAnimation.StartAnimation();
  }

  private void HandleDeathAnimationComplete()
  {
    imageAnimation.OnAnimationComplete = null;
    UIManager.Instance?.PlayCoinBlastForFish(this);
    UIManager.Instance?.PlayRainbowWinAnimation(this, data, pendingWinAmount);
    Die();
  }

  internal override void ResetFish()
  {
    base.ResetFish();
    deathSequencePlaying = false;
    pendingWinAmount = 0f;
    if (imageAnimation != null)
      imageAnimation.OnAnimationComplete = null;
  }
}
