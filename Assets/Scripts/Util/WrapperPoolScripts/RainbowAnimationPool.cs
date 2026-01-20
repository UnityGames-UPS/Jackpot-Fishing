internal class RainbowAnimationPool : GenericObjectPool<RainbowWinAnimationView>
{
  internal static RainbowAnimationPool Instance;

  internal override void Start()
  {
    Instance = this;
    base.Start();
  }
}
