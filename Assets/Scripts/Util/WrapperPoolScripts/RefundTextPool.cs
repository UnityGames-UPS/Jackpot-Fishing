public class RefundTextPool : GenericObjectPool<RefundTextPopup>
{
  internal static RefundTextPool Instance;

  internal override void Start()
  {
    Instance = this;
    base.Start();
  }
}
