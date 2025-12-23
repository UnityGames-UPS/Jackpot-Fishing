public class BulletPool : GenericObjectPool<BulletView>
{
  internal static BulletPool Instance;

  internal override void Awake()
  {
    base.Awake();
    Instance = this; 
  }
}
