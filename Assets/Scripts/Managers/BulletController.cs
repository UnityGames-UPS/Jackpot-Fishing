using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class BulletController : MonoBehaviour
{
  [SerializeField] private float speed = 20f;
  [SerializeField] private float lifeTime = 4f;

  private Vector2 lastVelocity;
  private Rigidbody2D rb;
  private float lifeTimer;
  private bool active;

  private BulletPool pool;

  void Awake()
  {
    rb = GetComponent<Rigidbody2D>();
    rb.gravityScale = 0;
    rb.freezeRotation = true;
  }

  internal void InitBullet(BulletPool ownerPool)
  {
    pool = ownerPool;
  }

  internal void Fire(Vector2 direction)
  {
    direction.Normalize();

    rb.velocity = direction * speed;
    UpdateVisual(direction);

    lifeTimer = lifeTime;
    active = true;
  }

  void FixedUpdate()
  {
    if (!active) return;

    lastVelocity = rb.velocity;

    lifeTimer -= Time.fixedDeltaTime;
    if (lifeTimer <= 0f)
      ReturnToPool();
  }

  void OnCollisionEnter2D(Collision2D collision)
  {
    if (!active) return;

    if (collision.collider.CompareTag("Wall"))
    {
      Reflect(collision.contacts[0].normal);
    }
    else if (collision.collider.CompareTag("Fish"))
    {
      // damage fish here
      ReturnToPool();
    }
  }

  void Reflect(Vector2 normal)
  {
    float speedMagnitude = lastVelocity.magnitude;

    if (speedMagnitude < 0.01f)
      return; // safety

    Vector2 reflectedDir = Vector2.Reflect(lastVelocity.normalized, normal);

    rb.velocity = reflectedDir * speedMagnitude;
    UpdateVisual(reflectedDir);
  }


  void UpdateVisual(Vector2 direction)
  {
    // sprite faces UP (-Y if your sprite is inverted)
    transform.up = -direction;
  }

  void ReturnToPool()
  {
    active = false;
    rb.velocity = Vector2.zero;
    pool.ReturnToPool(this);
  }
}
