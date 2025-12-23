using System.Collections;
using UnityEngine;

[RequireComponent(typeof(ImageAnimation))]
public class TorpedoBulletView : MonoBehaviour
{
  [Header("Movement")]
  [SerializeField] private float baseSpeed = 6f;
  [SerializeField] private float boostSpeed = 14f;
  [SerializeField] private float hitDistance = 0.2f;

  [Header("Animation Switch")]
  [Range(0.1f, 1f)]
  [SerializeField] private float switchPercent = 0.85f;

  [Header("Fake 3D Scale")]
  [SerializeField] private float pulseAmount = 0.1f;

  [Header("Animations")]
  [SerializeField] private Sprite[] sideAnim;
  [SerializeField] private Sprite[] backAnim;
  [SerializeField] private float sideAnimSpeed = 6f;
  [SerializeField] private float backAnimSpeed = 10f;

  private ImageAnimation imageAnimation;
  private Fish target;
  private Vector3 startPos;
  private float totalDistance;
  private float currentSpeed;
  private bool switched;
  private float BlastScaleFactor;

  internal void Init(Fish fish, float blastScaleFactor = 1f)
  {
    target = fish;
    BlastScaleFactor = blastScaleFactor;

    startPos = transform.position;
    totalDistance = Vector3.Distance(startPos, fish.transform.position);

    currentSpeed = baseSpeed;
    switched = false;

    imageAnimation = GetComponent<ImageAnimation>();
    imageAnimation.SetAnimationData(sideAnim, sideAnimSpeed, true);
    imageAnimation.StartAnimation();

    transform.localScale = Vector3.one;
    gameObject.SetActive(true);
  }

  void Update()
  {
    if (target == null)
    {
      ReturnToPool();
      return;
    }

    Move();
    CheckSwitch();
    UpdateScale();
    CheckHit();
  }
  
  void Move()
  {
    Vector3 dir = (target.transform.position - transform.position).normalized;
    transform.position += dir * currentSpeed * Time.deltaTime;
    transform.up = dir;
  }
  
  void CheckSwitch()
  {
    if (switched) return;

    float traveled = Vector3.Distance(startPos, transform.position);
    if (traveled / totalDistance >= switchPercent)
    {
      switched = true;
      currentSpeed = boostSpeed;

      imageAnimation.SetAnimationData(backAnim, backAnimSpeed, true);
      imageAnimation.StartAnimation();
    }
  }

  void UpdateScale()
  {
    if (switched) return;

    float traveled = Vector3.Distance(startPos, transform.position);
    float phaseDistance = totalDistance * switchPercent;

    if (phaseDistance <= 0.001f)
      return;

    float phaseProgress = Mathf.Clamp01(traveled / phaseDistance);
    float scaleT;

    if (phaseProgress <= 0.5f)
    {
      // Scale up (0 → 0.5)
      scaleT = phaseProgress / 0.5f;
    }
    else
    {
      // Scale down (0.5 → 1)
      scaleT = 1f - ((phaseProgress - 0.5f) / 0.5f);
    }

    float scale = Mathf.Lerp(1f, 1f + pulseAmount, scaleT);
    transform.localScale = new Vector3(scale, scale, 1f);
  }

  void CheckHit()
  {
    if (target == null || !target.gameObject.activeInHierarchy) return;
    if (Vector3.Distance(transform.position, target.transform.position) <= hitDistance)
    {
      BlastAnimation();
      ReturnToPool();
    }
  }

  void ReturnToPool()
  {
    imageAnimation.StopAnimation();
    target = null;
    TorpedoPool.Instance.ReturnToPool(this);
  }

  void BlastAnimation()
  {
    ImageAnimation animation = BlastAnimationPool.Instance.GetFromPool();
    animation.transform.SetPositionAndRotation(target.transform.position, Quaternion.identity);
    animation.rect.sizeDelta = target.Rect.sizeDelta * BlastScaleFactor;

    animation.OnAnimationComplete = () =>
    {
      BlastAnimationPool.Instance.ReturnToPool(animation);
    };

    animation.StartAnimation();
  }
}
