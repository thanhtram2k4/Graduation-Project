using UnityEngine;
using System.Collections;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public GameObject[] enemyPrefabs;
    public Transform[] spawnPoints; // 5 lanes
    public float spawnInterval = 5f;
    public float spawnIntervalDecrease = 0.1f;
    public float minSpawnInterval = 1f;

    [Header("Wave Settings")]
    public int currentWave = 0;
    public int enemiesPerWave = 5;
    public float waveCooldown = 10f;

    private void Start()
    {
        StartCoroutine(SpawnWaves());
    }

    private IEnumerator SpawnWaves()
    {
        yield return new WaitForSeconds(3f); // Thời gian chuẩn bị

        while (!GameManager.Instance.isGameOver && !GameManager.Instance.isGameWon)
        {
            currentWave++;
            Debug.Log($"Wave {currentWave} bắt đầu!");

            for (int i = 0; i < enemiesPerWave + currentWave; i++)
            {
                SpawnEnemy();
                yield return new WaitForSeconds(spawnInterval);
            }

            // Giảm thời gian spawn mỗi wave
            spawnInterval = Mathf.Max(minSpawnInterval, spawnInterval - spawnIntervalDecrease);

            yield return new WaitForSeconds(waveCooldown);
        }
    }

    private void SpawnEnemy()
    {
        if (enemyPrefabs.Length == 0 || spawnPoints.Length == 0) return;

        int randomEnemy = Random.Range(0, enemyPrefabs.Length);
        int randomLane = Random.Range(0, spawnPoints.Length);

        Instantiate(enemyPrefabs[randomEnemy], spawnPoints[randomLane].position, Quaternion.identity);
    }
}
