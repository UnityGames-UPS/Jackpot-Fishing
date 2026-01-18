public class BulletPool : GenericObjectPool<BulletView>
{
  internal static BulletPool Instance;

  internal override void Start()
  {
    base.Start();
    Instance = this; 
  }
}
