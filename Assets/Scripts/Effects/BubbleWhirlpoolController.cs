using System.Collections;
using UnityEngine;

public class BubbleWhirlpoolController : MonoBehaviour
{
  [Header("Pool")]
  [SerializeField] private BubbleWhirlpoolBubble bubblePrefab;
  [SerializeField] private int initialPoolSize = 12;

  [Header("Spawn")]
  [SerializeField] private float spawnInterval = 0.2f;
  [SerializeField] private float spawnRadius = 200f;

  [Header("Motion")]
  [SerializeField] private float radialSpeed = 100f;
  [SerializeField] private float angularSpeed = 50f;
  [SerializeField] private bool randomizeAngularDirection = true;
  [SerializeField] private float returnDistance = 0.1f;

  private BubbleWhirlpoolPool pool;
  private Coroutine spawnRoutine;
  private float spawnRadiusMultiplier = 1f;

  internal void Begin()
  {
    if (bubblePrefab == null)
    {
      Debug.LogWarning("[BubbleWhirlpoolController] Bubble prefab not set.");
      return;
    }

    EnsurePool();

    if (spawnRoutine != null)
      StopCoroutine(spawnRoutine);

    spawnRoutine = StartCoroutine(SpawnLoop());
  }

  internal void SetSpawnRadiusMultiplier(float multiplier)
  {
    spawnRadiusMultiplier = Mathf.Max(0f, multiplier);
  }

  internal void StopAndClear()
  {
    if (spawnRoutine != null)
    {
      StopCoroutine(spawnRoutine);
      spawnRoutine = null;
    }

    pool?.ReturnAllItemsToPool();
  }

  private void EnsurePool()
  {
    if (pool != null)
      return;

    GameObject poolRoot = new GameObject("BubbleWhirlpoolPool");
    poolRoot.transform.SetParent(transform, false);
    poolRoot.transform.localPosition = Vector3.zero;
    poolRoot.transform.localRotation = Quaternion.identity;
    poolRoot.transform.localScale = Vector3.one;
    pool = poolRoot.AddComponent<BubbleWhirlpoolPool>();
    pool.Configure(bubblePrefab, poolRoot.transform, initialPoolSize);
  }

  private IEnumerator SpawnLoop()
  {
    while (true)
    {
      SpawnBubble();
      yield return new WaitForSeconds(spawnInterval);
    }
  }

  private void SpawnBubble()
  {
    if (pool == null)
      return;

    BubbleWhirlpoolBubble bubble = pool.GetFromPool();
    if (bubble == null)
      return;

    float radius = spawnRadius * spawnRadiusMultiplier;
    float angle = Random.Range(0f, Mathf.PI * 2f);
    float radialSpeed = this.radialSpeed;
    float angularSpeedDeg = angularSpeed;
    if (randomizeAngularDirection && Random.value < 0.5f)
      angularSpeedDeg *= -1f;
    float angularSpeedRad = angularSpeedDeg * Mathf.Deg2Rad;
    bubble.Init(pool, radius, angle, radialSpeed, angularSpeedRad, returnDistance);
  }
}
