using UnityEngine;
using System.Collections;

/// <summary>
/// Spawns enemies using <see cref="ObjectPoolManager"/> (Rule 07).
/// Phase 3 note: Still uses hardcoded wave logic. Will be replaced by
/// LevelConfig.WaveData integration in C9.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public GameObject[] enemyPrefabs;
    public Transform[] spawnPoints;
    public float spawnInterval = 5f;
    public float spawnIntervalDecrease = 0.1f;
    public float minSpawnInterval = 1f;

    [Header("Wave Settings")]
    public int currentWave = 0;
    public int enemiesPerWave = 5;
    public float waveCooldown = 10f;

    private void Start()
    {
        // Pre-warm pools for all enemy prefabs
        if (ObjectPoolManager.Instance != null)
        {
            for (int i = 0; i < enemyPrefabs.Length; i++)
            {
                if (enemyPrefabs[i] != null)
                    ObjectPoolManager.Instance.CreatePool(enemyPrefabs[i], 10);
            }
        }

        StartCoroutine(SpawnWaves());
    }

    private IEnumerator SpawnWaves()
    {
        // No delay — spawn immediately for testing
        yield return null;

        while (!GameManager.Instance.isGameOver && !GameManager.Instance.isGameWon)
        {
            currentWave++;

            GameEventBus.Publish(new WaveStartedEvent
            {
                WaveIndex = currentWave - 1,
                TotalWaves = 0 // Will be set from LevelConfig in C9
            });

            for (int i = 0; i < enemiesPerWave + currentWave; i++)
            {
                SpawnEnemy();
                yield return new WaitForSeconds(spawnInterval);
            }

            spawnInterval = Mathf.Max(minSpawnInterval, spawnInterval - spawnIntervalDecrease);

            yield return new WaitForSeconds(waveCooldown);
        }
    }

    private void SpawnEnemy()
    {
        if (enemyPrefabs.Length == 0 || spawnPoints.Length == 0) return;

        int randomEnemy = Random.Range(0, enemyPrefabs.Length);
        int randomLane = Random.Range(0, spawnPoints.Length);

        // Pool.Get() instead of Instantiate (Rule 07)
        GameObject enemy = ObjectPoolManager.Instance.Get(enemyPrefabs[randomEnemy]);
        enemy.transform.position = spawnPoints[randomLane].position;
        enemy.transform.rotation = Quaternion.identity;
    }
}
