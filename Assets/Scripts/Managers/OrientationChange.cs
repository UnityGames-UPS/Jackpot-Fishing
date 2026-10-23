using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

// The host calls SendMessage('OC', 'SwitchDisplay', "<w>,<h>") on resize/rotation.
// The GameObject must stay named "OC".
public class OrientationChange : MonoBehaviour
{
  public static OrientationChange Instance;
  [SerializeField] private RectTransform UIWrapperPrimary;
  [SerializeField] private RectTransform UIWrapperSecondary;
  [SerializeField] private CanvasScaler CanvasScalerPrimary;
  [SerializeField] private CanvasScaler CanvasScalerSecondary;
  [SerializeField] private float transitionDuration = 0.2f;
  [SerializeField] private float waitForRotation = 0.2f;

  private Tween matchTweenPrimary;
  private Tween matchTweenSecondary;
  private Tween rotationTweenPrimary;
  private Tween rotationTweenSecondary;
  private Coroutine rotationRoutine;
  internal bool IsLandscape;
  internal int CurrentWidth;
  internal int CurrentHeight;

  private void Awake()
  {
    Instance = this;
  }

  private void Start()
  {
    ApplyMatch(Screen.width, Screen.height);
  }

  private void SwitchDisplay(string dimensions)
  {
    if (rotationRoutine != null) StopCoroutine(rotationRoutine);
    rotationRoutine = StartCoroutine(RotationCoroutine(dimensions));
  }

  private IEnumerator RotationCoroutine(string dimensions)
  {
    yield return new WaitForSecondsRealtime(waitForRotation);
    string[] parts = dimensions.Split(',');
    if (parts.Length == 2 && int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height) && width > 0 && height > 0)
    {
      ApplyMatch(width, height);
    }
    else
    {
      GameLog.Warn(LogCategory.Lifecycle, "Invalid format received in SwitchDisplay: " + dimensions);
    }
  }

  private void ApplyMatch(int width, int height)
  {
    IsLandscape = width > height;
    GameLog.Info(LogCategory.Lifecycle, $"Display {width}x{height} ({(IsLandscape ? "landscape" : "portrait")})");
    CurrentWidth = IsLandscape ? width : height;
    CurrentHeight = IsLandscape ? height : width;

    Quaternion targetRotation = IsLandscape ? Quaternion.identity : Quaternion.Euler(0, 0, -90);
    RotateWrapper(UIWrapperPrimary, targetRotation, ref rotationTweenPrimary);
    RotateWrapper(UIWrapperSecondary, targetRotation, ref rotationTweenSecondary);

    TweenMatch(CanvasScalerPrimary, width, height, ref matchTweenPrimary);
    TweenMatch(CanvasScalerSecondary, width, height, ref matchTweenSecondary);
  }

  private void RotateWrapper(RectTransform wrapper, Quaternion targetRotation, ref Tween tween)
  {
    if (wrapper == null) return;
    if (tween != null && tween.IsActive()) tween.Kill();
    tween = wrapper.DOLocalRotateQuaternion(targetRotation, transitionDuration).SetEase(Ease.OutCubic);
  }

  private void TweenMatch(CanvasScaler scaler, int width, int height, ref Tween tween)
  {
    if (scaler == null) return;

    float targetMatch = GetTargetMatch(scaler.referenceResolution, width, height);
    if (tween != null && tween.IsActive()) tween.Kill();
    tween = DOTween.To(() => scaler.matchWidthOrHeight, x => scaler.matchWidthOrHeight = x, targetMatch, transitionDuration).SetEase(Ease.InOutQuad);
  }

  // Continuous log-space match: picks the match value whose canvas scale fits the
  // reference rect fully inside the screen (rotated in portrait).
  private float GetTargetMatch(Vector2 referenceResolution, int width, int height)
  {
    float refW = referenceResolution.x;
    float refH = referenceResolution.y;

    float widthScale = (float)width / refW;
    float heightScale = (float)height / refH;

    float targetScale;
    if (IsLandscape)
    {
      targetScale = Mathf.Min(widthScale, heightScale);
    }
    else
    {
      float portraitWidthScale = (float)height / refW;
      float portraitHeightScale = (float)width / refH;
      targetScale = Mathf.Min(portraitWidthScale, portraitHeightScale);
    }

    if (Mathf.Abs(heightScale - widthScale) < 0.0001f)
      return 0.5f;

    float logRatio = Mathf.Log(heightScale / widthScale);
    float targetMatch = Mathf.Log(targetScale / widthScale) / logRatio;
    return Mathf.Clamp01(targetMatch);
  }

#if UNITY_EDITOR
  private void Update()
  {
    if (Input.GetKeyDown(KeyCode.Space))
    {
      SwitchDisplay(Screen.width + "," + Screen.height);
    }
  }
#endif
}
