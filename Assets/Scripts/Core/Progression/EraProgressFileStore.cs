using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

// =============================================================================
// EraProgressFileStore — disk persistence for EraProgressSaveData
//
// Plain C# class so the atomic-write and backup-fallback behaviour can be
// covered by Edit Mode tests against a temp folder (Rule 07 — Testability).
//
// Writes: tmp → File.Replace (previous file kept as backup), chained so they
//         land in order, off the main thread (Rule 06).
// Reads:  main file, falling back to the backup if the main file is corrupt.
// =============================================================================

/// <summary>
/// Reads and writes the era progression save file inside a single folder.
/// </summary>
public sealed class EraProgressFileStore
{
    private const string SaveFileName = "era_progress.json";
    private const string TempFileName = "era_progress.tmp";
    private const string BackupFileName = "era_progress.backup.json";

    private Task _pendingWrite = Task.CompletedTask;

    /// <summary>Folder holding the save, temp, and backup files.</summary>
    public string Folder { get; }

    /// <summary>Full path of the main save file.</summary>
    public string SavePath { get; }

    /// <summary>Full path of the backup (previous successful write).</summary>
    public string BackupPath { get; }

    private readonly string _tempPath;

    /// <summary>Creates a store rooted at <paramref name="folder"/>. Nothing is touched on disk yet.</summary>
    public EraProgressFileStore(string folder)
    {
        Folder = folder;
        SavePath = Path.Combine(folder, SaveFileName);
        BackupPath = Path.Combine(folder, BackupFileName);
        _tempPath = Path.Combine(folder, TempFileName);
    }

    /// <summary>
    /// Loads the save into <paramref name="into"/>, trying the main file then the backup.
    /// </summary>
    /// <param name="into">Buffer overwritten with the loaded data.</param>
    /// <param name="usedBackup">True if the main file was unusable and the backup was loaded.</param>
    /// <returns>False if neither file exists or parses.</returns>
    public bool TryLoad(EraProgressSaveData into, out bool usedBackup)
    {
        usedBackup = false;
        if (TryLoadFile(SavePath, into)) return true;

        usedBackup = TryLoadFile(BackupPath, into);
        return usedBackup;
    }

    /// <summary>Queues <paramref name="json"/> to be written atomically after any earlier writes.</summary>
    public void SaveAsync(string json)
    {
        string savePath = SavePath;
        string tempPath = _tempPath;
        string backupPath = BackupPath;

        _pendingWrite = _pendingWrite.ContinueWith(
            _ => WriteAtomic(json, savePath, tempPath, backupPath),
            TaskScheduler.Default);
    }

    /// <summary>Blocks until queued writes finish or <paramref name="timeoutMs"/> elapses.</summary>
    /// <returns>True if all writes finished in time.</returns>
    public bool Flush(int timeoutMs) => _pendingWrite.Wait(timeoutMs);

    private static bool TryLoadFile(string path, EraProgressSaveData into)
    {
        if (!File.Exists(path)) return false;

        try
        {
            JsonUtility.FromJsonOverwrite(File.ReadAllText(path), into);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EraProgressFileStore] Failed to parse '{path}': {e.Message}");
            return false;
        }
    }

    private static void WriteAtomic(string json, string savePath, string tempPath, string backupPath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(tempPath, json);

            if (File.Exists(savePath))
                File.Replace(tempPath, savePath, backupPath);
            else
                File.Move(tempPath, savePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[EraProgressFileStore] Failed to write era progress: {e.Message}");
        }
    }
}
