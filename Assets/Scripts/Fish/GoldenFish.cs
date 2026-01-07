
internal class GoldenFish : BaseFish
{
  internal override void Initialize(FishData data)
  {
    base.Initialize(data);
    SetupFallbackMovement();
  }
}
