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

  [Header("Phase-2 Homing")]
  [SerializeField] private float minHomingSpeed = 6f;
  [SerializeField] private float homingAcceleration = 25f;
  [SerializeField] private float maxPhase2Lifetime = 0.6f;
  [SerializeField] private float homingGain = 2.5f;
  [SerializeField] private float maxHomingSpeed = 18f;

  [Header("On Fish Hit")]
  [SerializeField] private Color fishDamageColor;

  private ImageAnimation imageAnimation;
  private BaseFish target;
  private bool finalized;
  private bool fishHit;

  private float phase2Timer;
  private float currentPhase2Speed;

  private Vector3 startPos;
  private Vector3 phase1EndPos;
  private Vector3 fireDir;

  private float phase1Distance;
  private float phase1Speed;
  private float traveled;

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

    finalized = false;
    fishHit = false;

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

    phase2Timer = 0f;
    currentPhase2Speed = minHomingSpeed;

    phase = TorpedoPhase.Phase2;
  }


  void UpdatePhase2()
  {
    phase2Timer += Time.deltaTime;

    // ⛔ Hard safety: Phase-2 can NEVER stall
    if (phase2Timer >= maxPhase2Lifetime)
    {
      ExplodeAt(transform.position);
      ReturnToPool();
      return;
    }

    Vector3 targetPos;

    // ---------------- TARGET RESOLUTION ----------------
    targetPos = GetBestTargetPoint();

    if (targetPos == Vector3.zero)
    {
      targetPos = transform.position; // explode where it is
    }


    Vector3 toTarget = targetPos - transform.position;
    float distance = toTarget.magnitude;

    if (distance <= hitDistance)
    {
      fishHit = true;

      if (target != null && target.data != null)
      {
        SocketIOManager.Instance.SendHitEvent(
          target.data.fishId,
          "torpedo",
          variant: target.data.variant
        );

        StartCoroutine(
          target.DamageAnimation(fishDamageColor) // or torpedo-specific color
        );
      }

      ExplodeAt(targetPos);
      ReturnToPool();
      return;
    }


    Vector3 dir = toTarget.normalized;

    // ---------------- TRUE HOMING SPEED ----------------
    float desiredSpeed = Mathf.Clamp(
      distance * homingGain,
      minHomingSpeed,
      maxHomingSpeed
    );

    currentPhase2Speed = Mathf.MoveTowards(
      currentPhase2Speed,
      desiredSpeed,
      homingAcceleration * Time.deltaTime
    );


    transform.position += dir * currentPhase2Speed * Time.deltaTime;

    // ---------------- ROTATION ----------------
    Quaternion targetRot =
      Quaternion.LookRotation(Vector3.forward, dir);

    transform.rotation = Quaternion.RotateTowards(
      transform.rotation,
      targetRot,
      rotationSpeed * Time.deltaTime
    );
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
    if (finalized) return;
    finalized = true;

    phase = TorpedoPhase.Finished;
    imageAnimation.StopAnimation();

    if (!fishHit)
    {
      SocketIOManager.Instance.SendHitEvent("", "torpedo");
    }

    target = null;
    TorpedoPool.Instance.ReturnToPool(this);
  }
  Vector3 GetBestTargetPoint()
  {
    if (target == null)
      return Vector3.zero;

    if (target.HitPoint != null && IsVisible(target.HitPoint.position))
      return target.HitPoint.position;

    BoxCollider2D col = target.GetComponent<BoxCollider2D>();
    if (col == null)
      return target.transform.position;

    Bounds b = col.bounds;
    Camera cam = Camera.main;

    // Clamp fish center to screen edge
    Vector3 vp = cam.WorldToViewportPoint(b.center);
    vp.x = Mathf.Clamp(vp.x, 0.05f, 0.95f);
    vp.y = Mathf.Clamp(vp.y, 0.05f, 0.95f);

    return cam.ViewportToWorldPoint(vp);
  }



  bool IsVisible(Vector3 worldPos)
  {
    Vector3 v = Camera.main.WorldToViewportPoint(worldPos);
    return v.z > 0 && v.x > 0 && v.x < 1 && v.y > 0 && v.y < 1;
  }

}
