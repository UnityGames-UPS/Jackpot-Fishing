using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;
using TMPro;

public class OrientationChange : MonoBehaviour
{
  public static OrientationChange Instance;
  [SerializeField] private RectTransform UIWrapperPrimary;
  [SerializeField] private RectTransform UIWrapperSecondary;
  [SerializeField] private CanvasScaler CanvasScalerPrimary;
  [SerializeField] private CanvasScaler CanvasScalerSecondary;
  [SerializeField] private float MatchWidth = 0f;
  [SerializeField] private float MatchHeight = 1f;
  [SerializeField] private float PortraitMatchHeight = 1f;
  [SerializeField] private float transitionDuration = 0.2f;
  [SerializeField] private float waitForRotation = 0.2f;


  private Vector2 ReferenceAspectPrimary;
  private Vector2 ReferenceAspectSecondary;
  private Tween matchTween;
  private Tween rotationTween;
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
    if (CanvasScalerPrimary != null) ReferenceAspectPrimary = CanvasScalerPrimary.referenceResolution;
    if (CanvasScalerSecondary != null) ReferenceAspectSecondary = CanvasScalerSecondary.referenceResolution;
    IsLandscape = Screen.width > Screen.height;
  }

  void SwitchDisplay(string dimensions)
  {
    if (rotationRoutine != null) StopCoroutine(rotationRoutine);
    rotationRoutine = StartCoroutine(RotationCoroutine(dimensions));
  }

  IEnumerator RotationCoroutine(string dimensions)
  {
    yield return new WaitForSecondsRealtime(waitForRotation);
    string[] parts = dimensions.Split(',');
    if (parts.Length == 2 && int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height) && width > 0 && height > 0)
    {
      Debug.LogWarning($"Unity: Received Dimensions - Width: {width}, Height: {height}");

      IsLandscape = width > height;
      CurrentWidth = IsLandscape ? width : height;
      CurrentHeight = IsLandscape ? height : width;

      Quaternion targetRotation = IsLandscape ? Quaternion.identity : Quaternion.Euler(0, 0, -90);
      if (rotationTween != null && rotationTween.IsActive()) rotationTween.Kill();
      if (UIWrapperPrimary != null)
      {
        rotationTween = UIWrapperPrimary.DOLocalRotateQuaternion(targetRotation, transitionDuration).SetEase(Ease.OutCubic);
      }
      if (UIWrapperSecondary != null)
      {
        UIWrapperSecondary.DOLocalRotateQuaternion(targetRotation, transitionDuration).SetEase(Ease.OutCubic);
      }

      float currentAspectRatio = IsLandscape ? (float)width / height : (float)height / width;
      float referenceAspectRatio = ReferenceAspectPrimary.y != 0 ? ReferenceAspectPrimary.x / ReferenceAspectPrimary.y : currentAspectRatio;
      Debug.LogWarning("currentAspect Ratio: " + currentAspectRatio);
      float targetMatch = GetTargetMatch(currentAspectRatio, referenceAspectRatio);

      if (matchTween != null && matchTween.IsActive()) matchTween.Kill();
      if (CanvasScalerPrimary != null)
      {
        matchTween = DOTween.To(() => CanvasScalerPrimary.matchWidthOrHeight, x => CanvasScalerPrimary.matchWidthOrHeight = x, targetMatch, transitionDuration).SetEase(Ease.InOutQuad);
      }
      if (CanvasScalerSecondary != null)
      {
        float referenceAspectRatioSecondary = ReferenceAspectSecondary.y != 0 ? ReferenceAspectSecondary.x / ReferenceAspectSecondary.y : currentAspectRatio;
        float targetMatchSecondary = GetTargetMatch(currentAspectRatio, referenceAspectRatioSecondary);
        DOTween.To(() => CanvasScalerSecondary.matchWidthOrHeight, x => CanvasScalerSecondary.matchWidthOrHeight = x, targetMatchSecondary, transitionDuration).SetEase(Ease.InOutQuad);
      }

      Debug.LogWarning($"matchWidthOrHeight set to: {targetMatch}");
    }
    else
    {
      Debug.LogWarning("Unity: Invalid format received in SwitchDisplay");
    }
  }

  private float GetTargetMatch(float currentAspectRatio, float referenceAspectRatio)
  {
    float targetMatch;
    if (IsLandscape)
    {
      targetMatch = currentAspectRatio > referenceAspectRatio ? MatchHeight : MatchWidth;
    }
    else
    {
      if (currentAspectRatio >= 1.3f && currentAspectRatio < 1.4f)
        targetMatch = 0.27f;   // ~1.3
      else if (currentAspectRatio >= 1.4f && currentAspectRatio < 1.5f)
        targetMatch = 0.32f;   // ~1.4
      else if (currentAspectRatio >= 1.5f && currentAspectRatio < 1.6f)
        targetMatch = 0.34f;   // ~1.5
      else if (currentAspectRatio >= 1.6f && currentAspectRatio < 1.85f)
        targetMatch = 0.42f;    // ~2.0 range
      else if (currentAspectRatio >= 1.85 && currentAspectRatio < 2.4)
        targetMatch = 0.5f;
      else
        targetMatch = PortraitMatchHeight;
    }

    return targetMatch;
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
