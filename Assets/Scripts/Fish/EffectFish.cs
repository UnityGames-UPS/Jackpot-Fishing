internal class EffectFish : BaseFish
{
  internal override void Initialize(FishData data)
  {
    base.Initialize(data);
    SetupFallbackMovement();
  }
}

