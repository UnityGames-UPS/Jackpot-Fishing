using UnityEngine;

public abstract class BaseGun : MonoBehaviour
{
  protected Transform muzzle;

  internal virtual void Awake()
  {
    muzzle = transform.GetChild(0);
  }

  internal virtual void UpdateAim(Vector3 worldPos)
  {
    RotateGun(worldPos);
  }

  protected void RotateGun(Vector3 worldPos)
  {
    Vector3 dir = worldPos - transform.position;
    if (dir.sqrMagnitude < 0.001f) return;
    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
    angle = Mathf.Clamp(angle, -90f, 90f);
    transform.rotation = Quaternion.Euler(0, 0, angle);
  }

  internal abstract void Fire();
}
