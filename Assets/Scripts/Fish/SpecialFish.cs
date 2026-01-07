
// SpecialFish class
internal class SpecialFish : BaseFish
{
  internal override void Initialize(FishData data)
  {
    base.Initialize(data);
    SetupFallbackMovement();
  }

}

