using System.Collections;
using UnityEngine;

[RequireComponent(typeof(ImageAnimation))]
public class TorpedoBulletView : MonoBehaviour
{
  private enum Phase { Phase1, Phase2, Finished }

  [Header("Phase 1")]
  [SerializeField] float phase1Duration = 0.18f;
  [SerializeField] float pulseAmount = 0.8f;

  [Header("Phase 2")]
  [SerializeField] float minSpeed = 6f;
  [SerializeField] float maxSpeed = 18f;
  [SerializeField] float accel = 25f;
  [SerializeField] float maxLifetime = 0.6f;
  [SerializeField] float hitDistance = 0.1f;

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
  private Vector3 fireDir;

  private float phase1Speed;
  private float phase2Speed;
  private float timer;
  private Phase phase;
  private bool finished;

  // ---------------- INIT ----------------

  internal void Init(BaseFish fish)
  {
    anim = GetComponent<ImageAnimation>();

    target = fish;
    startPos = transform.position;

    lastKnownTargetPos = fish.HitPoint.position;
    fireDir = (lastKnownTargetPos - startPos).normalized;

    float dist = Vector3.Distance(startPos, lastKnownTargetPos);
    phase1Speed = dist / phase1Duration;
    phase1End = startPos + fireDir * dist * 0.5f;

    anim.SetAnimationData(sideAnim, animSpeed, true);
    anim.StartAnimation();

    phase2Speed = minSpeed;
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
    float t = Mathf.PingPong(Time.time * 4f, 1f);
    float scale = Mathf.Lerp(1f, 1f + pulseAmount, t);
    transform.localScale = Vector3.one * scale;
  }

  // ---------------- PHASE 2 ----------------

  void BeginPhase2()
  {
    transform.localScale = Vector3.one;

    anim.SetAnimationData(backAnim, animSpeed, true);
    anim.StartAnimation();

    phase = Phase.Phase2;
    timer = 0f;
  }

  void UpdatePhase2()
  {
    timer += Time.deltaTime;

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

    float desiredSpeed = Mathf.Clamp(dist * 2.5f, minSpeed, maxSpeed);
    phase2Speed = Mathf.MoveTowards(
      phase2Speed,
      desiredSpeed,
      accel * Time.deltaTime
    );

    transform.position += dir * phase2Speed * Time.deltaTime;

    Quaternion rot = Quaternion.LookRotation(Vector3.forward, dir);
    transform.rotation = Quaternion.RotateTowards(
      transform.rotation,
      rot,
      rotationSpeed * Time.deltaTime
    );
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
