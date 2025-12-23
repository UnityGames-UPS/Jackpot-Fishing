using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class BulletView : MonoBehaviour
{
  [SerializeField] private float speed = 20f;
  [SerializeField] private float lifeTime = 4f;
  [SerializeField] private float wallPushDistance = 0.05f;


  [Header("Debug")]
  [SerializeField] private bool drawDebugRay = false;
  private Vector2 velocity;
  private Rigidbody2D rb;
  private float lifeTimer;
  private bool active;

  void Awake()
  {
    rb = GetComponent<Rigidbody2D>();
    rb.gravityScale = 0;
    rb.freezeRotation = true;
    rb.bodyType = RigidbodyType2D.Kinematic;
  }

  internal void Fire(Vector2 direction)
  {
    velocity = direction.normalized * speed;
    rb.position = transform.position;

    UpdateVisual(direction);

    lifeTimer = lifeTime;
    active = true;
  }


  void FixedUpdate()
  {
    if (!active) return;

    float delta = Time.fixedDeltaTime;
    Vector2 currentPos = rb.position;
    Vector2 nextPos = currentPos + velocity * delta;

    // Raycast for wall hit
    RaycastHit2D hit = Physics2D.Raycast(
        currentPos,
        velocity.normalized,
        velocity.magnitude * delta,
        LayerMask.GetMask("Wall")
    );

    DebugRay(currentPos, delta);

    if (hit)
    {
      ReflectFromHit(hit);
      nextPos = hit.point + hit.normal * wallPushDistance;
    }

    rb.MovePosition(nextPos);

    lifeTimer -= delta;
    if (lifeTimer <= 0f)
      ReturnToPool();
  }


  void OnTriggerEnter2D(Collider2D other)
  {
    if (!active) return;

    if (other.CompareTag("Fish"))
    {
      HitMarkerPool.Instance.GetFromPool().Play(transform.position);

      if (other.TryGetComponent<NormalFish>(out var fish))
        fish.DamageAnimation();

      ReturnToPool();
    }
  }

  void ReflectFromHit(RaycastHit2D hit)
  {
    Vector2 dir = velocity.normalized;

    Vector2 reflectedDir;

    // Corner-safe reflection
    if (Mathf.Abs(hit.normal.x) > 0.5f && Mathf.Abs(hit.normal.y) > 0.5f)
    {
      reflectedDir = new Vector2(-dir.x, -dir.y);
    }
    else
    {
      reflectedDir = Vector2.Reflect(dir, hit.normal);
    }

    velocity = reflectedDir.normalized * speed;
    UpdateVisual(velocity.normalized);
  }


  void UpdateVisual(Vector2 direction)
  {
    transform.up = direction;
  }

  void ReturnToPool()
  {
    active = false;
    rb.velocity = Vector2.zero;
    BulletPool.Instance.ReturnToPool(this);
  }

  void DebugRay(Vector2 currentPos, float delta)
  {
#if UNITY_EDITOR
    if (drawDebugRay)
    {
      Debug.DrawRay(
          currentPos,
          velocity.normalized * velocity.magnitude * delta,
          Color.red,
          5f
      );
    }
#endif
  }
}
