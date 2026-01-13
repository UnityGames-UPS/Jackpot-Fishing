public class RefundTextPool : GenericObjectPool<RefundTextPopup>
{
  internal static RefundTextPool Instance;

  internal override void Awake()
  {
    Instance = this;
    base.Awake();
  }
}
