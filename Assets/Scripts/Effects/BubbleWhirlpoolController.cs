using System.Collections;
using UnityEngine;

public class BubbleWhirlpoolController : MonoBehaviour
{
  [Header("Pool")]
  [SerializeField] private BubbleWhirlpoolBubble bubblePrefab;
  [SerializeField] private int initialPoolSize = 24;

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
  private Coroutine radialSpeedLerpRoutine;
  private Coroutine angularSpeedLerpRoutine;
  private float spawnRadiusMultiplier = 1f;
  private float baseSpawnInterval;
  private float baseRadialSpeed;
  private float baseAngularSpeed;
  private int angularDirectionSign = 1;

  private void Awake()
  {
    baseSpawnInterval = spawnInterval;
    baseRadialSpeed = radialSpeed;
    baseAngularSpeed = angularSpeed;
  }

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

    angularDirectionSign = randomizeAngularDirection && Random.value < 0.5f ? -1 : 1;
    spawnRoutine = StartCoroutine(SpawnLoop());
  }

  internal void SetSpawnRadiusMultiplier(float multiplier)
  {
    spawnRadiusMultiplier = Mathf.Max(0f, multiplier);
  }

  internal void SetSpawnIntervalMultiplier(float multiplier)
  {
    spawnInterval = baseSpawnInterval * multiplier;
  }

  internal void ResetSpawnInterval()
  {
    spawnInterval = baseSpawnInterval;
  }

  internal void SetRadialSpeedMultiplier(float multiplier)
  {
    radialSpeed = baseRadialSpeed * Mathf.Max(0.01f, multiplier);
  }

  internal void LerpRadialSpeedMultiplier(float targetMultiplier, float duration)
  {
    if (radialSpeedLerpRoutine != null)
      StopCoroutine(radialSpeedLerpRoutine);

    radialSpeedLerpRoutine = StartCoroutine(LerpRadialSpeedRoutine(
      Mathf.Max(0.01f, targetMultiplier),
      Mathf.Max(0f, duration)
    ));
  }

  internal void ResetRadialSpeed()
  {
    if (radialSpeedLerpRoutine != null)
    {
      StopCoroutine(radialSpeedLerpRoutine);
      radialSpeedLerpRoutine = null;
    }
    radialSpeed = baseRadialSpeed;
  }

  internal void SetAngularSpeedMultiplier(float multiplier)
  {
    angularSpeed = baseAngularSpeed * Mathf.Max(0.01f, multiplier);
  }

  internal void LerpAngularSpeedMultiplier(float targetMultiplier, float duration)
  {
    if (angularSpeedLerpRoutine != null)
      StopCoroutine(angularSpeedLerpRoutine);

    angularSpeedLerpRoutine = StartCoroutine(LerpAngularSpeedRoutine(
      Mathf.Max(0.01f, targetMultiplier),
      Mathf.Max(0f, duration)
    ));
  }

  internal void ResetAngularSpeed()
  {
    if (angularSpeedLerpRoutine != null)
    {
      StopCoroutine(angularSpeedLerpRoutine);
      angularSpeedLerpRoutine = null;
    }
    angularSpeed = baseAngularSpeed;
  }

  private IEnumerator LerpAngularSpeedRoutine(float targetMultiplier, float duration)
  {
    float startMultiplier = baseAngularSpeed > 0f ? angularSpeed / baseAngularSpeed : 1f;
    float timer = 0f;
    if (duration <= Mathf.Epsilon)
    {
      SetAngularSpeedMultiplier(targetMultiplier);
      angularSpeedLerpRoutine = null;
      yield break;
    }

    while (timer < duration)
    {
      timer += Time.deltaTime;
      float t = Mathf.Clamp01(timer / duration);
      float current = Mathf.Lerp(startMultiplier, targetMultiplier, t);
      SetAngularSpeedMultiplier(current);
      yield return null;
    }

    SetAngularSpeedMultiplier(targetMultiplier);
    angularSpeedLerpRoutine = null;
  }

  private IEnumerator LerpRadialSpeedRoutine(float targetMultiplier, float duration)
  {
    float startMultiplier = baseRadialSpeed > 0f ? radialSpeed / baseRadialSpeed : 1f;
    float timer = 0f;
    if (duration <= Mathf.Epsilon)
    {
      SetRadialSpeedMultiplier(targetMultiplier);
      radialSpeedLerpRoutine = null;
      yield break;
    }

    while (timer < duration)
    {
      timer += Time.deltaTime;
      float t = Mathf.Clamp01(timer / duration);
      float current = Mathf.Lerp(startMultiplier, targetMultiplier, t);
      SetRadialSpeedMultiplier(current);
      yield return null;
    }

    SetRadialSpeedMultiplier(targetMultiplier);
    radialSpeedLerpRoutine = null;
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
    float angularSpeedDeg = angularSpeed * angularDirectionSign;
    float angularSpeedRad = angularSpeedDeg * Mathf.Deg2Rad;
    bubble.Init(pool, radius, angle, radialSpeed, angularSpeedRad, returnDistance);
  }

  internal int GetAngularDirectionSign()
  {
    return angularDirectionSign;
  }
}
