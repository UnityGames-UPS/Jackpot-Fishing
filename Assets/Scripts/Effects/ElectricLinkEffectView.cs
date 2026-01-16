using UnityEngine;

internal class ElectricLinkEffectView : MonoBehaviour
{
  private enum LengthAxis
  {
    X,
    Y
  }

  [SerializeField] private RectTransform maskRoot;
  [SerializeField] private ImageAnimation linkAnimation;
  [SerializeField] private RectTransform linkRect;
  [SerializeField] private Camera worldCamera;
  [SerializeField] private float minLinkDistance = 20f;
  [SerializeField] private LengthAxis lengthAxis = LengthAxis.X;
  [SerializeField] private bool allowStretchForLongLinks = true;
  [SerializeField] private float rotationOffsetDegrees = 0f;
  private Vector2 baseSize;

  private void Awake()
  {
    if (maskRoot == null)
      maskRoot = GetComponent<RectTransform>();
    if (linkAnimation == null)
      linkAnimation = GetComponentInChildren<ImageAnimation>();
    if (linkRect == null && linkAnimation != null)
      linkRect = linkAnimation.GetComponent<RectTransform>();

    if (linkRect != null)
      baseSize = linkRect.sizeDelta;

    worldCamera = Camera.main;
  }

  internal void StartAnimation()
  {
    if (linkAnimation == null)
      return;

    linkAnimation.OnAnimationComplete = null;
    linkAnimation.StartAnimation();
  }

  internal void StopAnimation()
  {
    linkAnimation?.StopAnimation();
  }

  internal void UpdateLink(Vector3 start, Vector3 end, float height)
  {
    if (maskRoot == null)
      return;

    RectTransform parentRect = maskRoot.parent as RectTransform;
    if (parentRect == null)
      return;

    Vector2 screenStart = RectTransformUtility.WorldToScreenPoint(worldCamera, start);
    Vector2 screenEnd = RectTransformUtility.WorldToScreenPoint(worldCamera, end);

    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
          parentRect, screenStart, worldCamera, out Vector2 localStart) ||
        !RectTransformUtility.ScreenPointToLocalPointInRectangle(
          parentRect, screenEnd, worldCamera, out Vector2 localEnd))
      return;

    Vector2 dir = localEnd - localStart;
    float distance = dir.magnitude;
    if (distance <= Mathf.Max(0.01f, minLinkDistance))
      return;

    Vector2 mid = localStart + dir * 0.5f;
    maskRoot.anchoredPosition = mid;
    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + rotationOffsetDegrees;
    maskRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
    maskRoot.sizeDelta = lengthAxis == LengthAxis.X
      ? new Vector2(distance, height)
      : new Vector2(height, distance);

    if (linkRect == null)
      return;

    if (baseSize.x <= 0f || baseSize.y <= 0f)
      baseSize = linkRect.sizeDelta;

    if (lengthAxis == LengthAxis.X)
    {
      float scaleX = baseSize.x > 0f ? distance / baseSize.x : 1f;
      float appliedScaleX = allowStretchForLongLinks ? Mathf.Max(1f, scaleX) : 1f;
      linkRect.localScale = new Vector3(appliedScaleX, 1f, 1f);
    }
    else
    {
      float scaleY = baseSize.y > 0f ? distance / baseSize.y : 1f;
      float appliedScaleY = allowStretchForLongLinks ? Mathf.Max(1f, scaleY) : 1f;
      linkRect.localScale = new Vector3(1f, appliedScaleY, 1f);
    }
  }
}
