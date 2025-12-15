using UnityEngine;

public abstract class BaseGun : MonoBehaviour
{
  [Header("Common References")]
  [SerializeField] protected Transform gunPivot;
  [SerializeField] protected Transform muzzle;
  [SerializeField] protected GunManager GunManager;

  internal virtual void UpdateAim(Vector3 worldPos)
  {
    RotateGun(worldPos);
  }

  protected void RotateGun(Vector3 worldPos)
  {
    Vector3 dir = worldPos - gunPivot.position;
    if (dir.sqrMagnitude < 0.001f) return;
    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
    angle = Mathf.Clamp(angle, -90f, 90f);
    gunPivot.rotation = Quaternion.Euler(0, 0, angle);
  }

  internal abstract void Fire();
}
