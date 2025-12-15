using UnityEngine;

public class GunManager : MonoBehaviour
{
  [SerializeField] private BaseGun currentGun;
  [SerializeField] private RectTransform bgRect;
  [SerializeField] private Camera worldCamera;
  internal void UpdateAim(Vector3 screenPos)
  {
    if (!RectTransformUtility.RectangleContainsScreenPoint(
                bgRect, screenPos, worldCamera))
      return;

    RectTransformUtility.ScreenPointToWorldPointInRectangle(
        bgRect, screenPos, worldCamera, out Vector3 worldPos);

    worldPos.z = 0;

    currentGun.UpdateAim(worldPos);
  }

  internal void SetFiring(bool firing)
  {
    if (firing)
      currentGun.Fire();
  }

  internal void SetGun(BaseGun newGun)
  {
    if (currentGun != null)
      currentGun.gameObject.SetActive(false);

    currentGun = newGun;
    currentGun.gameObject.SetActive(true);
  }
}
