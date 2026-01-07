
// JackpotFish class
internal class JackpotFish : BaseFish
{
  internal override void Initialize(FishData data)
  {
    base.Initialize(data);
    SetupFallbackMovement();
  }
}

