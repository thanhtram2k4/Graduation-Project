using UnityEngine;
using System.Collections;

/// <summary>
/// Data-driven enemy spawner that reads wave definitions from
/// <see cref="LevelConfig.waves"/> and only activates during
/// <see cref="LevelState.Defending"/> (Rule 01, Rule 07).
///
/// <para><b>Lifecycle:</b> Subscribes to <see cref="LevelStateChangedEvent"/>
/// via <see cref="GameEventBus"/>. When the level transitions to Defending,
/// starts <see cref="SpawnWavesRoutine"/>. Tracks active enemy count and
/// publishes <see cref="VictoryEvent"/> when all waves are cleared and
/// no enemies remain on the field.</para>
///
/// <para><b>Win-state tracking:</b> Increments <c>_activeEnemiesCount</c>
/// on each spawn, decrements on <see cref="EnemyDestroyedEvent"/> and
/// <see cref="BaseTakeDamageEvent"/> (enemy reached base). When the final
/// wave finishes spawning AND <c>_activeEnemiesCount</c> reaches zero,
/// evaluates star rating from <see cref="LevelConfig.EvaluateStars"/>
/// and publishes <see cref="VictoryEvent"/>.</para>
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Configuration (read from LevelConfig at runtime)
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Spawn Points")]
    [Tooltip("One Transform per lane, indexed by lane index. " +
             "Position is used as the world-space spawn origin for that lane.")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Timing")]
    [Tooltip("Seconds to wait after the last enemy is killed before publishing " +
             "VictoryEvent. Allows LaneSweeper charge animations and enemy death " +
             "animations to finish playing before Time.timeScale freezes the scene.")]
    [Min(0f)]
    [SerializeField] private float victoryDelay = 3.5f;

    // ─────────────────────────────────────────────────────────────────────────
    // Runtime State
    // ─────────────────────────────────────────────────────────────────────────

    private Coroutine _spawnCoroutine;
    private int _activeEnemiesCount;
    private int _currentWaveIndex;
    private bool _allWavesSpawned;
    private bool _victoryPublished;
    private bool _hasStartedSpawning;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        GameEventBus.OnLevelStateChanged += HandleLevelStateChanged;
        GameEventBus.OnEnemyDestroyed += HandleEnemyDestroyed;
        GameEventBus.OnBaseTakeDamage += HandleBaseTakeDamage;
    }

    private void OnDisable()
    {
        GameEventBus.OnLevelStateChanged -= HandleLevelStateChanged;
        GameEventBus.OnEnemyDestroyed -= HandleEnemyDestroyed;
        GameEventBus.OnBaseTakeDamage -= HandleBaseTakeDamage;
    }

    private void Start()
    {
        // Pre-warm pools as early as possible. GetLevelConfig() resolves
        // Just-In-Time from CampaignManager or GameManager, avoiding
        // Script Execution Order race conditions.
        LevelConfig config = GetLevelConfig();
        if (config != null)
        {
            PreWarmPools(config);
        }
        else
        {
            Debug.LogWarning("[EnemySpawner] LevelConfig not yet available in Start(). " +
                             "Pool pre-warming deferred to first Defending transition.", this);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Just-In-Time Config Resolution
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the current LevelConfig at call time, avoiding Script Execution
    /// Order issues with CampaignManager / GameManager initialization.
    /// Checks CampaignManager first (DDOL singleton), then GameManager.
    /// </summary>
    private LevelConfig GetLevelConfig()
    {
        if (CampaignManager.Instance != null)
            return CampaignManager.Instance.CurrentLevelConfig;

        if (GameManager.Instance != null)
            return GameManager.Instance.currentLevelConfig;

        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Pool Pre-Warming (Rule 07)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pre-warms object pools for every enemy prefab referenced in the
    /// level's wave definitions. Prevents first-spawn allocation spikes.
    /// </summary>
    private void PreWarmPools(LevelConfig config)
    {
        if (ObjectPoolManager.Instance == null || config == null) return;

        for (int w = 0; w < config.waves.Count; w++)
        {
            WaveData wave = config.waves[w];
            for (int e = 0; e < wave.spawnEntries.Count; e++)
            {
                EnemySpawnEntry entry = wave.spawnEntries[e];
                if (entry.enemyData != null && entry.enemyData.unitPrefab != null)
                {
                    ObjectPoolManager.Instance.CreatePool(
                        entry.enemyData.unitPrefab, entry.count);
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reacts to level state transitions. Starts spawning ONLY when the
    /// state becomes <see cref="LevelState.Defending"/> (Rule 01).
    /// </summary>
    private void HandleLevelStateChanged(LevelStateChangedEvent evt)
    {
        if (evt.NewState == LevelState.Defending)
        {
            LevelConfig config = GetLevelConfig();
            if (config == null)
            {
                Debug.LogError("[EnemySpawner] Cannot start spawning — " +
                               "LevelConfig is null.", this);
                return;
            }

            // Deferred pool pre-warming if Start() ran before config was ready
            PreWarmPools(config);

            // Only start if not already spawning (guard against duplicate events).
            if (_spawnCoroutine == null)
            {
                _allWavesSpawned = false;
                _victoryPublished = false;
                _hasStartedSpawning = false;
                // _currentWaveIndex is preserved between waves.
                // It is only reset to 0 on the very first Defending entry
                // (when coming from Shuffling/Preparing for the first time).
                _spawnCoroutine = StartCoroutine(SpawnWavesRoutine());
            }
        }
    }

    /// <summary>
    /// Decrements active enemy count when an enemy is destroyed by a troop.
    /// Checks for victory condition after decrement.
    /// </summary>
    private void HandleEnemyDestroyed(EnemyDestroyedEvent evt)
    {
        _activeEnemiesCount--;
        CheckVictoryCondition();
    }

    /// <summary>
    /// Reacts to base damage events. Does NOT decrement _activeEnemiesCount
    /// here because BaseHealthManager also calls ForceKill() on the enemy,
    /// which triggers HandleDeath → EnemyDestroyedEvent → HandleEnemyDestroyed.
    /// Decrementing in BOTH handlers would double-count, causing premature
    /// victory when the count drops below zero.
    /// </summary>
    private void HandleBaseTakeDamage(BaseTakeDamageEvent evt)
    {
        // Count decrement handled exclusively by HandleEnemyDestroyed.
        // This handler remains subscribed for future use (analytics, UI).
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Spawning Coroutine
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Iterates through all waves defined in <see cref="LevelConfig.waves"/>,
    /// respecting <see cref="WaveData.delayBeforeWave"/> and
    /// <see cref="WaveData.spawnInterval"/>. Publishes wave events for
    /// UI and audio subscribers.
    /// </summary>
    private IEnumerator SpawnWavesRoutine()
    {
        LevelConfig config = GetLevelConfig();

        // Guard: abort if no waves are defined — designer must add wave data.
        if (config == null || config.waves == null || config.waves.Count == 0)
        {
            Debug.LogError("[EnemySpawner] LevelConfig has no waves defined! " +
                           "Add at least one WaveData entry to the level config. " +
                           "Spawner will not run.", this);
            _spawnCoroutine = null;
            yield break;
        }

        int totalWaves = config.TotalWaves;
        int startWave = _currentWaveIndex;

        for (int w = startWave; w < totalWaves; w++)
        {
            _currentWaveIndex = w;
            WaveData wave = config.waves[w];

            // Wait for the pre-wave delay (player preparation time).
            if (wave.delayBeforeWave > 0f)
            {
                yield return new WaitForSeconds(wave.delayBeforeWave);
            }

            // Publish wave start for UI counter and audio stinger.
            GameEventBus.Publish(new WaveStartedEvent
            {
                WaveIndex = w,
                TotalWaves = totalWaves
            });

            // Spawn all entries in this wave. Each entry has its own
            // spawnDelay (relative to wave start) and count.
            // We process entries concurrently by starting sub-coroutines.
            int entriesRemaining = wave.spawnEntries.Count;
            for (int e = 0; e < wave.spawnEntries.Count; e++)
            {
                StartCoroutine(SpawnEntryRoutine(wave.spawnEntries[e],
                    wave.spawnInterval, () => entriesRemaining--));
            }

            // Wait until all entries in this wave have finished spawning.
            while (entriesRemaining > 0)
            {
                yield return null;
            }

            // Wait until all enemies from this wave are resolved before
            // moving to the next wave (Rule 01: wave ends when all enemies
            // in the current wave are defeated or have reached the exit).
            while (_activeEnemiesCount > 0)
            {
                yield return null;
            }

            // Publish wave completion.
            bool isFinalWave = (w == totalWaves - 1);
            GameEventBus.Publish(new WaveCompletedEvent
            {
                WaveIndex = w,
                TotalWaves = totalWaves,
                IsFinalWave = isFinalWave
            });

            // If more waves remain, LevelStateManager may transition back
            // to Preparing. We yield one frame to let that happen, then
            // wait for the next Defending transition before continuing.
            if (!isFinalWave)
            {
                // Stop this coroutine — a new Defending transition will
                // restart SpawnWavesRoutine with the next wave index.
                _currentWaveIndex = w + 1;
                _spawnCoroutine = null;
                yield break;
            }
        }

        // All waves have been fully spawned.
        _allWavesSpawned = true;
        _spawnCoroutine = null;
        CheckVictoryCondition();
    }

    /// <summary>
    /// Spawns a single <see cref="EnemySpawnEntry"/>: waits for
    /// <see cref="EnemySpawnEntry.spawnDelay"/>, then spawns
    /// <see cref="EnemySpawnEntry.count"/> enemies with
    /// <paramref name="spawnInterval"/> between each.
    /// </summary>
    private IEnumerator SpawnEntryRoutine(EnemySpawnEntry entry,
        float spawnInterval, System.Action onComplete)
    {
        if (entry.enemyData == null || entry.enemyData.unitPrefab == null)
        {
            Debug.LogWarning("[EnemySpawner] Skipping spawn entry with " +
                             "null enemyData or unitPrefab.");
            onComplete?.Invoke();
            yield break;
        }

        // Wait for this entry's individual spawn delay.
        if (entry.spawnDelay > 0f)
        {
            yield return new WaitForSeconds(entry.spawnDelay);
        }

        // Resolve spawn position from the lane's spawn point.
        Vector3 spawnPos = GetSpawnPosition(entry.laneIndex);

        for (int i = 0; i < entry.count; i++)
        {
            SpawnEnemy(entry.enemyData, spawnPos, entry.laneIndex);

            // Wait between consecutive units (except after the last one).
            if (i < entry.count - 1 && spawnInterval > 0f)
            {
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        onComplete?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Spawn Helper
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a single enemy via <see cref="ObjectPoolManager"/> (Rule 07).
    /// Increments <see cref="_activeEnemiesCount"/>.
    /// </summary>
    private void SpawnEnemy(EnemyUnitData enemyData, Vector3 position, int laneIndex)
    {
        GameObject enemy = ObjectPoolManager.Instance.Get(enemyData.unitPrefab);
        enemy.transform.position = position;
        enemy.transform.rotation = Quaternion.identity;

        _activeEnemiesCount++;
        _hasStartedSpawning = true;
    }

    /// <summary>
    /// Returns the world-space spawn position for the given lane index.
    /// Falls back to the first spawn point if the lane index exceeds the
    /// spawnPoints array length.
    /// </summary>
    private Vector3 GetSpawnPosition(int laneIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[EnemySpawner] No spawn points assigned!", this);
            return transform.position;
        }

        if (laneIndex >= 0 && laneIndex < spawnPoints.Length
            && spawnPoints[laneIndex] != null)
        {
            return spawnPoints[laneIndex].position;
        }

        Debug.LogWarning($"[EnemySpawner] No spawn point for lane {laneIndex}. " +
                         "Using spawn point [0] as fallback.");
        return spawnPoints[0].position;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Victory Condition
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Checks if all waves have been spawned and all enemies are resolved.
    /// If so, starts a delayed coroutine that waits <see cref="victoryDelay"/>
    /// seconds before publishing <see cref="VictoryEvent"/>, allowing
    /// LaneSweeper charge and enemy death animations to finish.
    /// Guard: only fires once per session (<see cref="_victoryPublished"/>).
    /// </summary>
    private void CheckVictoryCondition()
    {
        if (_victoryPublished) return;
        if (!_hasStartedSpawning) return;
        if (!_allWavesSpawned) return;
        if (_activeEnemiesCount > 0) return;

        // Set flag immediately to prevent duplicate coroutine starts.
        _victoryPublished = true;

        StartCoroutine(DelayedVictoryRoutine());
    }

    /// <summary>
    /// Waits <see cref="victoryDelay"/> seconds for cinematic cleanup
    /// (sweeper charge animation, enemy death animations) before
    /// evaluating stars and publishing <see cref="VictoryEvent"/>.
    /// Uses WaitForSeconds so it respects Time.timeScale — if a
    /// DefeatEvent freezes time during the delay, the victory is
    /// naturally cancelled.
    /// </summary>
    private IEnumerator DelayedVictoryRoutine()
    {
        Debug.Log($"[EnemySpawner] All enemies cleared. Victory in {victoryDelay}s...");

        yield return new WaitForSeconds(victoryDelay);

        // Evaluate star rating from current Base HP.
        int starsEarned = 3;
        LevelConfig victoryConfig = GetLevelConfig();
        if (victoryConfig != null)
        {
            var baseHealth = FindAnyObjectByType<BaseHealthManager>();
            if (baseHealth != null)
            {
                float hpFraction = (float)baseHealth.CurrentHP / victoryConfig.baseMaxHP;
                starsEarned = victoryConfig.EvaluateStars(hpFraction);
            }
        }

        GameEventBus.Publish(new VictoryEvent
        {
            StarsEarned = starsEarned,
            Score = 0
        });

        Debug.Log($"[EnemySpawner] Victory! Stars earned: {starsEarned}");
    }
}
