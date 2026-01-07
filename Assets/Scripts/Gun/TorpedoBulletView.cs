using UnityEngine;

[RequireComponent(typeof(ImageAnimation))]
public class TorpedoBulletView : MonoBehaviour
{
  private enum TorpedoPhase
  {
    Phase1,
    Phase2,
    Finished
  }

  [Header("Movement")]
  [SerializeField] private float hitDistance = 0.1f;

  [Header("Phase Durations (constant time)")]
  [SerializeField] private float phase1Duration = 0.18f;
  [SerializeField] private float phase2Duration = 0.22f;

  [Header("Rotation")]
  [SerializeField] private float rotationSpeed = 720f; // deg/sec

  [Header("Fake 3D Scale")]
  [SerializeField] private float pulseAmount = 0.8f;

  [Header("Animations")]
  [SerializeField] private Sprite[] sideAnim;
  [SerializeField] private Sprite[] backAnim;
  [SerializeField] private float sideAnimSpeed = 19f;
  [SerializeField] private float backAnimSpeed = 19f;

  [Header("Phase-1 Distance")]
  [Range(0.1f, 1f)]
  [SerializeField] private float phase1Percent = 0.5f;
  [SerializeField] private float minPhase1Distance = 0.4f;
  [SerializeField] private float maxPhase1Distance = 3.5f;

  [Header("Phase-2 Correction")]
  [SerializeField] private float snapCorrectionDistance = 0.4f;
  [SerializeField] private float snapStrength = 0.5f; // 0–1

  private ImageAnimation imageAnimation;
  private BaseFish target;

  private Vector3 startPos;
  private Vector3 phase1EndPos;
  private Vector3 phase2TargetPos;
  private Vector3 fireDir;

  private float phase1Distance;
  private float phase1Speed;
  private float phase2Speed;
  private float traveled;
  private bool phase2HasValidFish;

  private TorpedoPhase phase;

  // ---------------- INIT ----------------

  internal void Init(BaseFish fish, Vector3 direction)
  {
    imageAnimation = GetComponent<ImageAnimation>();

    target = fish;
    fireDir = direction.normalized;

    startPos = transform.position;

    float fishDistance = Vector3.Distance(
      startPos,
      fish.HitPoint.transform.position
    );

    phase1Distance = fishDistance * phase1Percent;
    phase1Distance = Mathf.Clamp(
      phase1Distance,
      minPhase1Distance,
      maxPhase1Distance
    );

    // 🔑 constant-time phase 1
    phase1Speed = phase1Distance / phase1Duration;

    phase1EndPos = startPos + fireDir * phase1Distance;

    traveled = 0f;
    phase = TorpedoPhase.Phase1;

    transform.localScale = Vector3.one;

    imageAnimation.SetAnimationData(sideAnim, sideAnimSpeed, true);
    imageAnimation.StartAnimation();

    gameObject.SetActive(true);
  }

  // ---------------- UPDATE ----------------

  void Update()
  {
    if (phase == TorpedoPhase.Finished)
      return;

    if (phase == TorpedoPhase.Phase1)
      UpdatePhase1();
    else
      UpdatePhase2();
  }

  // ---------------- PHASE 1 ----------------

  void UpdatePhase1()
  {
    float step = phase1Speed * Time.deltaTime;

    transform.position = Vector3.MoveTowards(
      transform.position,
      phase1EndPos,
      step
    );

    transform.up = fireDir;

    traveled += step;
    UpdateScale(traveled / phase1Distance);

    if (Vector3.Distance(transform.position, phase1EndPos) <= 0.01f)
    {
      BeginPhase2();
    }
  }

  void UpdateScale(float t)
  {
    t = Mathf.Clamp01(t);

    float scaleT = t <= 0.5f
      ? t / 0.5f
      : 1f - ((t - 0.5f) / 0.5f);

    float scale = Mathf.Lerp(1f, 1f + pulseAmount, scaleT);
    transform.localScale = new Vector3(scale, scale, 1f);
  }

  // ---------------- PHASE 2 ----------------

  void BeginPhase2()
  {
    transform.localScale = Vector3.one;

    imageAnimation.SetAnimationData(backAnim, backAnimSpeed, true);
    imageAnimation.StartAnimation();

    if (target != null && target.gameObject.activeInHierarchy)
    {
      phase2TargetPos = target.HitPoint.transform.position;
      phase2HasValidFish = true;
    }
    else
    {
      phase2TargetPos = Vector3.zero; // screen center
      phase2HasValidFish = false;
    }

    float phase2Distance = Vector3.Distance(
      transform.position,
      phase2TargetPos
    );

    // 🔑 constant-time phase 2
    phase2Speed = phase2Distance / phase2Duration;

    phase = TorpedoPhase.Phase2;
  }

  void UpdatePhase2()
  {
    Vector3 targetPos = phase2TargetPos;

    if (
      phase2HasValidFish &&
      target != null &&
      target.gameObject.activeInHierarchy
    )
    {
      float distToLockedTarget =
        Vector3.Distance(transform.position, phase2TargetPos);

      if (distToLockedTarget <= snapCorrectionDistance)
      {
        Vector3 liveFishPos = target.HitPoint.transform.position;
        targetPos = Vector3.Lerp(
          phase2TargetPos,
          liveFishPos,
          snapStrength
        );
      }
    }

    Vector3 dir = (targetPos - transform.position).normalized;

    float step = phase2Speed * Time.deltaTime;
    transform.position += dir * step;

    // smooth rotation
    Quaternion targetRot =
      Quaternion.LookRotation(Vector3.forward, dir);

    transform.rotation = Quaternion.RotateTowards(
      transform.rotation,
      targetRot,
      rotationSpeed * Time.deltaTime
    );

    if (Vector3.Distance(transform.position, targetPos) <= hitDistance)
    {
      ExplodeAt(targetPos);
      ReturnToPool();
    }
  }

  // ---------------- BLAST ----------------

  void ExplodeAt(Vector3 pos)
  {
    ImageAnimation blast = BlastAnimationPool.Instance.GetFromPool();
    blast.transform.SetPositionAndRotation(pos, Quaternion.identity);

    blast.OnAnimationComplete = () =>
    {
      BlastAnimationPool.Instance.ReturnToPool(blast);
    };

    blast.StartAnimation();
  }

  // ---------------- CLEANUP ----------------

  void ReturnToPool()
  {
    phase = TorpedoPhase.Finished;
    imageAnimation.StopAnimation();
    target = null;
    TorpedoPool.Instance.ReturnToPool(this);
  }
}
