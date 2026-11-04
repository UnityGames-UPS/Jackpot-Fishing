using UnityEngine;

public abstract class BaseGun : MonoBehaviour
{
  protected Transform muzzle => transform.GetChild(0);
  internal Transform GetMuzzleTransform() => muzzle;

  internal virtual void UpdateAim(Vector3 worldPos)
  {
    RotateGun(worldPos);
  }

  // Clamped in the parent's space, so it holds for portrait (rotated wrapper) and top seats
  // (rig turned 180°) alike: the gun never points behind its own rig.
  protected void RotateGun(Vector3 worldPos)
  {
    Vector3 dir = worldPos - transform.position;
    if (transform.parent != null)
      dir = transform.parent.InverseTransformDirection(dir);
    if (dir.sqrMagnitude < 0.001f) return;
    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
    angle = Mathf.Clamp(Mathf.DeltaAngle(0f, angle), -90f, 90f);
    transform.localRotation = Quaternion.Euler(0, 0, angle);
  }

  internal abstract void Fire();
}
