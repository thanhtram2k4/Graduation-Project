using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// =============================================================================
// EraProgressionManager — persistent owner of unlocked eras and heroes
//
// DontDestroyOnLoad singleton. Listens for correct Ông Bụt answers and unlocks
// a still-locked hero from the answered question's era. Unlock logic lives in
// EraProgressionState, disk I/O in EraProgressFileStore; this class handles
// lifecycle and events.
//
// PERSISTENCE: Application.persistentDataPath/<saveFolderName>/era_progress.json
//   (see EraProgressFileStore for the atomic write / backup contract).
//
// EVENT BUS: GameEventBus.Reset() clears every subscriber on scene reload,
//   including this persistent object's. The subscription is therefore
//   re-registered on SceneManager.sceneLoaded.
//
// LAYER: Gameplay. UI reacts to HeroUnlockedEvent / EraUnlockedEvent (Rule 07).
// =============================================================================

/// <summary>
/// Tracks unlocked <see cref="EraType"/>s and <see cref="HeroCardData"/>s across
/// scenes and sessions, and unlocks heroes when Ông Bụt questions are answered correctly.
/// </summary>
public class EraProgressionManager : MonoBehaviour
{
    // ── Singleton ───────────────────────────────────────────────────────────

    /// <summary>The persistent instance, or null before the first scene initialises it.</summary>
    public static EraProgressionManager Instance { get; private set; }

    // ── Configuration ───────────────────────────────────────────────────────

    [Header("Hero Catalog")]
    [Tooltip("Resources sub-folder containing every HeroCardData asset.")]
    [SerializeField] private string heroCardResourcesPath = "Data/HeroCards";

    [Header("Starting Progress")]
    [Tooltip("Eras unlocked on a fresh save.")]
    [SerializeField] private EraType[] startingEras = { EraType.History };

    [Tooltip("Heroes unlocked on a fresh save, so the first draft is never empty.")]
    [SerializeField] private HeroCardData[] startingHeroes;

    [Header("Persistence")]
    [Tooltip("Folder under Application.persistentDataPath that holds era_progress.json.")]
    [SerializeField] private string saveFolderName = "SaveData";

    private const int FlushOnQuitTimeoutMs = 2000;

    // ── Runtime State ───────────────────────────────────────────────────────

    private readonly EraProgressionState _state = new EraProgressionState();
    private readonly EraProgressSaveData _saveBuffer = new EraProgressSaveData();
    private HeroCardData[] _heroCatalog = Array.Empty<HeroCardData>();
    private EraProgressFileStore _store;

    // Cached so subscribe/unsubscribe never allocates a new delegate.
    private Action<OngButAnswerResultEvent> _answerResultHandler;

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Every HeroCardData asset loaded from Resources. Do not modify.</summary>
    public HeroCardData[] HeroCatalog => _heroCatalog;

    /// <summary>Returns true if <paramref name="era"/> is unlocked.</summary>
    public bool IsEraUnlocked(EraType era) => _state.IsEraUnlocked(era);

    /// <summary>Returns true if <paramref name="hero"/> is unlocked.</summary>
    public bool IsHeroUnlocked(HeroCardData hero) => _state.IsHeroUnlocked(hero);

    /// <summary>Number of heroes currently unlocked.</summary>
    public int UnlockedHeroCount => _state.UnlockedHeroCount;

    /// <summary>
    /// Appends every unlocked hero to <paramref name="results"/>. Pass a reused
    /// list to avoid allocation.
    /// </summary>
    public void GetUnlockedHeroes(List<HeroCardData> results) =>
        _state.GetUnlockedHeroes(_heroCatalog, results);

    /// <summary>
    /// Adds <paramref name="hero"/> to the persistent unlocked list, saves, and
    /// publishes <see cref="HeroUnlockedEvent"/>.
    /// </summary>
    /// <returns>True if newly unlocked; false if null, missing a Hero ID, or already unlocked.</returns>
    public bool UnlockHero(HeroCardData hero)
    {
        if (!_state.UnlockHero(hero)) return false;

        SaveAsync();
        GameEventBus.Publish(new HeroUnlockedEvent { Hero = hero, Era = hero.eraType });
        Debug.Log($"[EraProgressionManager] Unlocked hero '{hero.heroID}' ({hero.eraType}).");
        return true;
    }

    /// <summary>
    /// Unlocks <paramref name="era"/>, saves, and publishes <see cref="EraUnlockedEvent"/>.
    /// </summary>
    /// <returns>True if newly unlocked; false if it already was.</returns>
    public bool UnlockEra(EraType era)
    {
        if (!_state.UnlockEra(era)) return false;

        SaveAsync();
        GameEventBus.Publish(new EraUnlockedEvent { Era = era });
        Debug.Log($"[EraProgressionManager] Unlocked era {era}.");
        return true;
    }

    // ── Test seams (visible to test assemblies via InternalsVisibleTo) ──────

    /// <summary>Full path of the main save file.</summary>
    internal string SaveFilePath => _store.SavePath;

    /// <summary>Replaces the Resources-loaded catalog with <paramref name="catalog"/>.</summary>
    internal void OverrideHeroCatalog(HeroCardData[] catalog) =>
        _heroCatalog = catalog ?? Array.Empty<HeroCardData>();

    /// <summary>Blocks until queued save writes finish or the timeout elapses.</summary>
    internal bool FlushPendingSaves(int timeoutMs) => _store.Flush(timeoutMs);

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _answerResultHandler = HandleAnswerResult;
        _store = new EraProgressFileStore(Path.Combine(Application.persistentDataPath, saveFolderName));
        _heroCatalog = Resources.LoadAll<HeroCardData>(heroCardResourcesPath);

        LoadFromDisk();
        ApplyStartingProgress();
    }

    private void OnEnable()
    {
        // A duplicate destroyed in Awake still receives OnEnable this frame.
        if (Instance != this) return;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        SubscribeToEventBus();
    }

    private void OnDisable()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        GameEventBus.OnOngButAnswerResult -= _answerResultHandler;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnApplicationQuit()
    {
        // Give any in-flight write a chance to land before the process exits.
        _store.Flush(FlushOnQuitTimeoutMs);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EVENT HANDLERS
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => SubscribeToEventBus();

    /// <summary>Unlocks a random locked hero from the question's era on a correct answer.</summary>
    private void HandleAnswerResult(OngButAnswerResultEvent evt)
    {
        if (!evt.IsCorrect) return;

        HeroCardData hero = _state.PickLockedHero(evt.Era, _heroCatalog, UnityEngine.Random.value);
        if (hero == null)
        {
            Debug.Log($"[EraProgressionManager] Correct answer in {evt.Era}, but every hero of that era is already unlocked.");
            return;
        }

        UnlockHero(hero);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INTERNAL HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Removes then adds the handler so repeated calls never double-subscribe.</summary>
    private void SubscribeToEventBus()
    {
        GameEventBus.OnOngButAnswerResult -= _answerResultHandler;
        GameEventBus.OnOngButAnswerResult += _answerResultHandler;
    }

    /// <summary>Ensures configured starting eras/heroes are unlocked. No events: nothing is listening yet.</summary>
    private void ApplyStartingProgress()
    {
        bool changed = false;

        if (startingEras != null)
        {
            for (int i = 0; i < startingEras.Length; i++)
                changed |= _state.UnlockEra(startingEras[i]);
        }

        if (startingHeroes != null)
        {
            for (int i = 0; i < startingHeroes.Length; i++)
                changed |= _state.UnlockHero(startingHeroes[i]);
        }

        if (changed) SaveAsync();
    }

    private void LoadFromDisk()
    {
        // Neither file usable → keep the empty default state.
        if (!_store.TryLoad(_saveBuffer, out bool usedBackup)) return;

        if (usedBackup)
            Debug.LogWarning("[EraProgressionManager] Main save unreadable; restored from backup.");

        if (_saveBuffer.saveFormatVersion > EraProgressionState.CurrentSaveFormatVersion)
            Debug.LogWarning("[EraProgressionManager] Era progress was saved by a newer game version; loading best-effort.");

        _state.LoadFrom(_saveBuffer);
    }

    /// <summary>Snapshots state to JSON on the main thread; the store writes it off-thread.</summary>
    private void SaveAsync()
    {
        _state.WriteTo(_saveBuffer);
        _store.SaveAsync(JsonUtility.ToJson(_saveBuffer, true));
    }
}
