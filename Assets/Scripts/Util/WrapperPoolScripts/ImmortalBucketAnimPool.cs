internal class ImmortalBucketAnimPool : GenericObjectPool<ImmortalBucketAnimView>
{
  internal static ImmortalBucketAnimPool Instance;

  internal override void Start()
  {
    Instance = this;
    base.Start();
  }
}
