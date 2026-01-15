using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(RectTransform))]
public class ImageAnimation : MonoBehaviour
{
  public enum ImageState
  {
    NONE,
    PLAYING,
    PAUSED
  }
  internal RectTransform rect;
  internal ImageState currentAnimationState;
  public List<Sprite> textureArray;
  public Image rendererDelegate;
  public bool useSharedMaterial = true;
  public bool doLoopAnimation = true;
  private int indexOfTexture;
  private float idealFrameRate = 0.0416666679f;
  private float delayBetweenAnimation;
  public float AnimationSpeed = 5f;
  public float delayBetweenLoop;
  public bool PlayOnAwake = false;
  public Action OnAnimationComplete;

  void OnValidate()
  {
    if (rendererDelegate == null)
    {
      rendererDelegate = GetComponent<Image>();
    }
  }

  private void Awake()
  {
    rect = GetComponent<RectTransform>();
  }

  private void OnEnable()
  {
    if (PlayOnAwake)
    {
      StartAnimation();
    }
  }

  private void OnDisable()
  {
    StopAnimation();
  }

  private void AnimationProcess()
  {
    SetTextureOfIndex();
    indexOfTexture++;
    if (indexOfTexture == textureArray.Count)
    {
      indexOfTexture = 0;
      if (doLoopAnimation)
      {
        Invoke("AnimationProcess", delayBetweenAnimation + delayBetweenLoop);
      }
      else
      {
        currentAnimationState = ImageState.NONE;
        OnAnimationComplete?.Invoke();
      }
    }
    else
    {
      Invoke("AnimationProcess", delayBetweenAnimation);
    }
  }

  public void StartAnimation()
  {
    if (currentAnimationState != ImageState.PLAYING)
    {
      indexOfTexture = 0;
      RevertToInitialState();
      delayBetweenAnimation = idealFrameRate * (float)textureArray.Count / AnimationSpeed;
      currentAnimationState = ImageState.PLAYING;
      Invoke("AnimationProcess", delayBetweenAnimation);
    }
  }

  public void PauseAnimation()
  {
    if (currentAnimationState == ImageState.PLAYING)
    {
      CancelInvoke("AnimationProcess");
      currentAnimationState = ImageState.PAUSED;
    }
  }

  public void ResumeAnimation()
  {
    if (currentAnimationState == ImageState.PAUSED && !IsInvoking("AnimationProcess"))
    {
      Invoke("AnimationProcess", delayBetweenAnimation);
      currentAnimationState = ImageState.PLAYING;
    }
  }

  internal void SetAnimationData(Sprite[] frames, float speed, bool loop)
  {
    textureArray = new List<Sprite>(frames);
    AnimationSpeed = speed;
    doLoopAnimation = loop;
  }

  internal void SetAnimationSpeed(float speed)
  {
    AnimationSpeed = Mathf.Max(0.01f, speed);
    delayBetweenAnimation = idealFrameRate * (float)textureArray.Count / AnimationSpeed;

    if (currentAnimationState == ImageState.PLAYING)
    {
      CancelInvoke("AnimationProcess");
      Invoke("AnimationProcess", delayBetweenAnimation);
    }
  }

  public void StopAnimation()
  {
    if (currentAnimationState != 0)
    {
      if (textureArray.Count > 0)
        rendererDelegate.sprite = textureArray[0];
      CancelInvoke("AnimationProcess");
      currentAnimationState = ImageState.NONE;
    }
  }

  public void RevertToInitialState()
  {
    indexOfTexture = 0;
    SetTextureOfIndex();
  }

  private void SetTextureOfIndex()
  {
    if (textureArray.Count == 0) return;
    if (useSharedMaterial)
    {
      rendererDelegate.sprite = textureArray[indexOfTexture];
    }
    else
    {
      rendererDelegate.sprite = textureArray[indexOfTexture];
    }
  }
}
