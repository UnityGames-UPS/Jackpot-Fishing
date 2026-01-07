internal class JackpotDragon : BaseFish
{
  internal override void Initialize(FishData data)
  {
    base.Initialize(data);
    SetupFallbackMovement();
  }  
}
