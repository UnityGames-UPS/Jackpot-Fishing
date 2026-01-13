using System.Collections;
using UnityEngine;

[RequireComponent(typeof(ImageAnimation))]
public class TorpedoBulletView : MonoBehaviour
{
  private enum Phase { Phase1, Phase2, Finished }

  [Header("Phase 1")]
  [SerializeField] float phase1Duration = 0.18f;
  [SerializeField, Range(0f, 1f)] float phase1DistanceFactor = 0.2f;
  [SerializeField] float pulseAmount = 0.8f;

  [Header("Phase 2")]
  [SerializeField] float phase2Speed = 12f;
  [SerializeField] float maxLifetime = 0.6f;
  [SerializeField] float hitDistance = 0.1f;
  [SerializeField, Range(0f, 1f)] float phase2ScaleDownDistanceFactor = 0.2f;

  [Header("Visuals")]
  [SerializeField] Sprite[] sideAnim;
  [SerializeField] Sprite[] backAnim;
  [SerializeField] float animSpeed = 19f;
  [SerializeField] float rotationSpeed = 720f;

  [Header("On Fish Hit")]
  [SerializeField] private Color fishDamageColor = new Color(1f, 0.35f, 0.35f, 1f);

  private ImageAnimation anim;
  private BaseFish target;
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

  // ---------------- INIT ----------------

  internal void Init(BaseFish fish)
  {
    anim = GetComponent<ImageAnimation>();
    anim.OnAnimationComplete = null;

    target = fish;
    startPos = transform.position;

    lastKnownTargetPos = fish.HitPoint.position;
    fireDir = (lastKnownTargetPos - startPos).normalized;

    float dist = Vector3.Distance(startPos, lastKnownTargetPos);
    phase1Distance = dist * phase1DistanceFactor;
    phase1Speed = phase1Distance / phase1Duration;
    phase1End = startPos + fireDir * phase1Distance;

    float backAnimSpeed = GetBackAnimSpeedForPhase1();
    anim.SetAnimationData(backAnim, backAnimSpeed, false);
    anim.StartAnimation();

    timer = 0f;
    phase = Phase.Phase1;
    finished = false;

    gameObject.SetActive(true);
  }

  // ---------------- UPDATE ----------------

  void Update()
  {
    if (finished) return;

    if (phase == Phase.Phase1)
      UpdatePhase1();
    else if (phase == Phase.Phase2)
      UpdatePhase2();
  }

  // ---------------- PHASE 1 ----------------

  void UpdatePhase1()
  {
    transform.position = Vector3.MoveTowards(
      transform.position,
      phase1End,
      phase1Speed * Time.deltaTime
    );

    transform.up = fireDir;
    UpdatePulse();

    if (Vector3.Distance(transform.position, phase1End) <= 0.01f)
      BeginPhase2();
  }

  void UpdatePulse()
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

  // ---------------- PHASE 2 ----------------

  void BeginPhase2()
  {
    phase2StartPos = transform.position;
    phase2TotalDistance = Vector3.Distance(phase2StartPos, lastKnownTargetPos);

    phase = Phase.Phase2;
    timer = 0f;
  }

  void UpdatePhase2()
  {
    timer += Time.deltaTime;
    UpdatePhase2Scale();

    if (timer >= maxLifetime)
    {
      Explode(transform.position);
      Finish();
      return;
    }

    // 🔑 Update last known position if fish still exists
    if (target != null && target.gameObject.activeInHierarchy)
      lastKnownTargetPos = GetEdgeClampedTargetPos(target);

    Vector3 toTarget = lastKnownTargetPos - transform.position;
    float dist = toTarget.magnitude;

    if (dist <= hitDistance)
    {
      if (target != null)
      {
        StartCoroutine(target.DamageAnimation(fishDamageColor));
      }

      if (target != null && target.KillOnTorpedoArrival)
      {
        target.KillOnTorpedoArrival = false;
        target.OnKillingTorpedoArrived();
      }

      Explode(lastKnownTargetPos);
      Finish();
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

  void UpdatePhase2Scale()
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

  float GetBackAnimSpeedForPhase1()
  {
    if (backAnim == null || backAnim.Length == 0)
      return animSpeed;

    if (phase1Duration <= Mathf.Epsilon)
      return animSpeed;

    const float idealFrameRate = 0.0416666679f;
    float frameCount = backAnim.Length;
    return (idealFrameRate * frameCount * frameCount) / phase1Duration;
  }

  // ---------------- HELPERS ----------------

  Vector3 GetEdgeClampedTargetPos(BaseFish fish)
  {
    Camera cam = Camera.main;
    Vector3 vp = cam.WorldToViewportPoint(fish.HitPoint.position);

    vp.x = Mathf.Clamp(vp.x, 0.01f, 0.99f);
    vp.y = Mathf.Clamp(vp.y, 0.01f, 0.99f);

    return cam.ViewportToWorldPoint(vp);
  }

  void Explode(Vector3 pos)
  {
    var blast = BlastAnimationPool.Instance.GetFromPool();
    blast.transform.SetPositionAndRotation(pos, Quaternion.identity);
    blast.StartAnimation();
  }

  void Finish()
  {
    if (finished) return;
    finished = true;

    anim.OnAnimationComplete = null;
    anim.StopAnimation();
    target = null;

    StartCoroutine(ReturnNextFrame());
  }

  IEnumerator ReturnNextFrame()
  {
    yield return null;
    TorpedoPool.Instance.ReturnToPool(this);
  }
}
