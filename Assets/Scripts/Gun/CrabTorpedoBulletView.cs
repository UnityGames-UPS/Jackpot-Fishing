using System.Collections;
using UnityEngine;

[RequireComponent(typeof(ImageAnimation))]
public class CrabTorpedoBulletView : MonoBehaviour
{
  private enum Phase { Phase1, Phase2, Finished }

  [Header("Phase 1")]
  [SerializeField] private float phase1Duration = 0.18f;
  [SerializeField, Range(0f, 1f)] private float phase1DistanceFactor = 0.2f;
  [SerializeField, Range(0f, 1f)] private float phase1HeightRestoreDistanceFactor = 0.3f;
  [SerializeField] private float pulseAmount = 0.8f;

  [Header("Phase 2")]
  [SerializeField] private float phase2Speed = 12f;
  [SerializeField] private float maxLifetime = 0.6f;
  [SerializeField] private float hitDistance = 0.1f;
  [SerializeField, Range(0f, 1f)] private float phase2ScaleDownDistanceFactor = 0.2f;

  [Header("Visuals")]
  [SerializeField] private Sprite[] sideAnim;
  [SerializeField] private Sprite[] backAnim;
  [SerializeField] private float animSpeed = 19f;
  [SerializeField] private float rotationSpeed = 720f;
  [SerializeField] private float coinBlastScaleMultiplier = 1f;

  private ImageAnimation anim;
  private RectTransform rectTransform;
  private float initialHeight;
  private BaseFish target;
  private System.Action<BaseFish> onHit;
  private Vector3 lastKnownTargetPos;
  private Vector3 startPos;
  private Vector3 phase1End;
  private Vector3 phase2StartPos;
  private Vector3 fireDir;

  private float phase1Speed;
  private float phase1Distance;
  private float phase2TotalDistance;
  private float timer;
  private Phase phase;
  private bool finished;

  private void Awake()
  {
    rectTransform = GetComponent<RectTransform>();
    if (rectTransform != null)
      initialHeight = rectTransform.sizeDelta.y;
  }

  internal void Init(BaseFish fish, System.Action<BaseFish> onHitCallback)
  {
    anim = GetComponent<ImageAnimation>();
    anim.OnAnimationComplete = null;

    target = fish;
    onHit = onHitCallback;
    target?.RegisterIncomingTorpedo();
    startPos = transform.position;

    lastKnownTargetPos = fish != null ? fish.HitPoint.position : startPos;
    fireDir = (lastKnownTargetPos - startPos).normalized;

    float dist = Vector3.Distance(startPos, lastKnownTargetPos);
    phase1Distance = dist * phase1DistanceFactor;
    phase1Speed = phase1Distance / Mathf.Max(phase1Duration, 0.0001f);
    phase1End = startPos + fireDir * phase1Distance;

    float backAnimSpeed = GetBackAnimSpeedForPhase1();
    anim.SetAnimationData(backAnim, backAnimSpeed, false);
    anim.StartAnimation();

    SetHeight(0f);

    timer = 0f;
    phase = Phase.Phase1;
    finished = false;

    PlayLaunchBlast();
    gameObject.SetActive(true);
  }

  private void Update()
  {
    if (finished)
      return;

    if (phase == Phase.Phase1)
      UpdatePhase1();
    else if (phase == Phase.Phase2)
      UpdatePhase2();
  }

  private void UpdatePhase1()
  {
    transform.position = Vector3.MoveTowards(
      transform.position,
      phase1End,
      phase1Speed * Time.deltaTime
    );

    transform.up = fireDir;
    UpdateHeightRestore();
    UpdatePulse();

    if (Vector3.Distance(transform.position, phase1End) <= 0.01f)
      BeginPhase2();
  }

  private void UpdatePulse()
  {
    if (phase1Distance <= Mathf.Epsilon)
    {
      transform.localScale = Vector3.one;
      return;
    }

    float traveled = Vector3.Distance(startPos, transform.position);
    float progress = Mathf.Clamp01(traveled / phase1Distance);
    float scale = Mathf.Lerp(1f, 1f + pulseAmount, progress);
    transform.localScale = Vector3.one * scale;
  }

  private void UpdateHeightRestore()
  {
    if (rectTransform == null)
      return;

    if (phase1Distance <= Mathf.Epsilon)
    {
      SetHeight(initialHeight);
      return;
    }

    float traveled = Vector3.Distance(startPos, transform.position);
    float progress = Mathf.Clamp01(traveled / phase1Distance);
    float restoreProgress = phase1HeightRestoreDistanceFactor <= Mathf.Epsilon
      ? 1f
      : Mathf.Clamp01(progress / phase1HeightRestoreDistanceFactor);
    float height = Mathf.Lerp(0f, initialHeight, restoreProgress);
    SetHeight(height);
  }

  private void BeginPhase2()
  {
    phase2StartPos = transform.position;
    phase2TotalDistance = Vector3.Distance(phase2StartPos, lastKnownTargetPos);

    // anim.SetAnimationData(sideAnim, animSpeed, true);
    // anim.StartAnimation();

    phase = Phase.Phase2;
    timer = 0f;
  }

  private void UpdatePhase2()
  {
    timer += Time.deltaTime;
    UpdatePhase2Scale();

    if (timer >= maxLifetime)
    {
      ResolveImpact(transform.position);
      return;
    }

    if (target != null && target.gameObject.activeInHierarchy)
      lastKnownTargetPos = GetEdgeClampedTargetPos(target);

    Vector3 toTarget = lastKnownTargetPos - transform.position;
    float dist = toTarget.magnitude;

    if (dist <= hitDistance)
    {
      ResolveImpact(lastKnownTargetPos);
      return;
    }

    Vector3 dir = toTarget.normalized;
    transform.position += dir * phase2Speed * Time.deltaTime;

    Quaternion rot = Quaternion.LookRotation(Vector3.forward, dir);
    transform.rotation = Quaternion.RotateTowards(
      transform.rotation,
      rot,
      rotationSpeed * Time.deltaTime
    );
  }

  private void UpdatePhase2Scale()
  {
    if (phase2ScaleDownDistanceFactor <= Mathf.Epsilon)
    {
      transform.localScale = Vector3.one;
      return;
    }

    float traveled = Vector3.Distance(phase2StartPos, transform.position);
    float scaleDownDistance = Mathf.Max(
      phase2TotalDistance * phase2ScaleDownDistanceFactor,
      0.001f
    );
    float progress = Mathf.Clamp01(traveled / scaleDownDistance);
    float scale = Mathf.Lerp(1f + pulseAmount, 1f, progress);
    transform.localScale = Vector3.one * scale;
  }

  private float GetBackAnimSpeedForPhase1()
  {
    if (backAnim == null || backAnim.Length == 0)
      return animSpeed;

    if (phase1Duration <= Mathf.Epsilon)
      return animSpeed;

    const float idealFrameRate = 0.0416666679f;
    float frameCount = backAnim.Length;
    return (idealFrameRate * frameCount * frameCount) / phase1Duration;
  }

  private Vector3 GetEdgeClampedTargetPos(BaseFish fish)
  {
    Camera cam = Camera.main;
    if (cam == null)
      return fish.HitPoint.position;

    Vector3 vp = cam.WorldToViewportPoint(fish.HitPoint.position);
    vp.x = Mathf.Clamp(vp.x, 0.01f, 0.99f);
    vp.y = Mathf.Clamp(vp.y, 0.01f, 0.99f);

    return cam.ViewportToWorldPoint(vp);
  }

  private void PlayLaunchBlast()
  {
    if (BlastAnimationPool.Instance == null)
      return;

    var blast = BlastAnimationPool.Instance.GetFromPool();
    blast.transform.SetPositionAndRotation(transform.position, Quaternion.identity);
    blast.StartAnimation();
    blast.OnAnimationComplete = () =>
    {
      BlastAnimationPool.Instance.ReturnToPool(blast);
    };
  }

  private void ResolveImpact(Vector3 pos)
  {
    if (target != null)
      onHit?.Invoke(target);

    PlayCoinBlast(pos);
    Finish();
  }

  private void PlayCoinBlast(Vector3 pos)
  {
    if (CoinBlastAnimPool.Instance == null)
      return;

    var coinAnimation = CoinBlastAnimPool.Instance.GetFromPool();
    coinAnimation.transform.SetPositionAndRotation(pos, Quaternion.identity);
    coinAnimation.transform.localScale = Vector3.one * coinBlastScaleMultiplier;
  }

  private void Finish()
  {
    if (finished)
      return;

    finished = true;
    anim.OnAnimationComplete = null;
    anim.StopAnimation();
    target?.UnregisterIncomingTorpedo();
    target = null;
    onHit = null;
    SetHeight(0f);

    StartCoroutine(ReturnNextFrame());
  }

  private IEnumerator ReturnNextFrame()
  {
    yield return null;
    if (CrabTorpedoPool.Instance != null)
      CrabTorpedoPool.Instance.ReturnToPool(this);
  }

  private void SetHeight(float height)
  {
    if (rectTransform == null)
      return;

    Vector2 size = rectTransform.sizeDelta;
    size.y = height;
    rectTransform.sizeDelta = size;
  }
}
