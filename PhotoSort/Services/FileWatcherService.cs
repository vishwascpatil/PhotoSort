using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;

namespace PhotoSort.Services;

public sealed class FileWatcherService : IFileWatcherService
{
    private readonly ILogger<FileWatcherService> _logger;
    private readonly ConcurrentDictionary<string, System.IO.FileSystemWatcher> _watchers;
    private readonly ConcurrentDictionary<string, DateTime> _lastEventTimes;
    private readonly Timer _debounceTimer;
    private readonly object _batchLock;
    private readonly List<FileWatcherEventArgs> _pendingBatch;
    private bool _disposed;

    private static readonly HashSet<string> SupportedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".webp", ".heic", ".heif", ".raw", ".cr2",
        ".nef", ".arw", ".dng", ".orf", ".rw2", ".pef", ".sr2",
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp"
    ];

    private static readonly HashSet<string> IgnoredPaths =
    [
        ".DS_Store", "Thumbs.db", "desktop.ini", ".photothumb", ".phtcache"
    ];

    private const int DebounceMs = 500;
    private const int BatchFlushMs = 1000;

    public bool IsWatching => !_watchers.IsEmpty;

    public int WatchedFolderCount => _watchers.Count;

    public IReadOnlyList<string> WatchedFolders => _watchers.Keys.ToList().AsReadOnly();

    public event EventHandler<FileWatcherEventArgs>? FileChanged;
    public event EventHandler<FileSystemWatcher>? OverflowOccurred;

    public FileWatcherService(ILogger<FileWatcherService> logger)
    {
        _logger = logger;
        _watchers = new ConcurrentDictionary<string, FileSystemWatcher>(StringComparer.OrdinalIgnoreCase);
        _lastEventTimes = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        _batchLock = new object();
        _pendingBatch = [];
        _debounceTimer = new Timer(FlushBatch, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void StartWatching(string folderPath)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FileWatcherService));

        if (!System.IO.Directory.Exists(folderPath))
        {
            _logger.LogWarning("Cannot watch non-existent folder: {FolderPath}", folderPath);
            return;
        }

        if (_watchers.ContainsKey(folderPath))
        {
            _logger.LogDebug("Already watching folder: {FolderPath}", folderPath);
            return;
        }

        try
        {
            var watcher = new System.IO.FileSystemWatcher(folderPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = System.IO.NotifyFilters.FileName
                    | System.IO.NotifyFilters.LastWrite
                    | System.IO.NotifyFilters.Size
                    | System.IO.NotifyFilters.CreationTime
                    | System.IO.NotifyFilters.DirectoryName,
                Filter = "*.*",
                EnableRaisingEvents = false
            };

            watcher.Created += OnFileCreated;
            watcher.Changed += OnFileChanged;
            watcher.Deleted += OnFileDeleted;
            watcher.Renamed += OnFileRenamed;
            watcher.Error += OnError;

            if (_watchers.TryAdd(folderPath, watcher))
            {
                watcher.EnableRaisingEvents = true;
                _logger.LogInformation("Started watching folder: {FolderPath}", folderPath);
            }
            else
            {
                watcher.Dispose();
                _logger.LogWarning("Failed to add watcher for folder: {FolderPath}", folderPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start watching folder: {FolderPath}", folderPath);
        }
    }

    public void StartWatching(IEnumerable<string> folderPaths)
    {
        foreach (var path in folderPaths)
        {
            StartWatching(path);
        }
    }

    public void StopWatching()
    {
        StopWatchingAll();
    }

    public void StopWatching(string folderPath)
    {
        if (_watchers.TryRemove(folderPath, out var watcher))
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Created -= OnFileCreated;
                watcher.Changed -= OnFileChanged;
                watcher.Deleted -= OnFileDeleted;
                watcher.Renamed -= OnFileRenamed;
                watcher.Error -= OnError;
                watcher.Dispose();
                _logger.LogInformation("Stopped watching folder: {FolderPath}", folderPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping watcher for folder: {FolderPath}", folderPath);
            }
        }
    }

    public void StopWatchingAll()
    {
        var folders = _watchers.Keys.ToList();
        foreach (var folder in folders)
        {
            StopWatching(folder);
        }

        _lastEventTimes.Clear();

        lock (_batchLock)
        {
            _pendingBatch.Clear();
        }

        _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);

        _logger.LogInformation("Stopped all file watchers");
    }

    public bool IsWatchingFolder(string folderPath)
    {
        return _watchers.ContainsKey(folderPath);
    }

    private void OnFileCreated(object sender, System.IO.FileSystemEventArgs e)
    {
        HandleEvent(e.FullPath, FileWatcherChange.Created, watchedFolder: GetWatchedFolder(e.FullPath));
    }

    private void OnFileChanged(object sender, System.IO.FileSystemEventArgs e)
    {
        HandleEvent(e.FullPath, FileWatcherChange.Modified, watchedFolder: GetWatchedFolder(e.FullPath));
    }

    private void OnFileDeleted(object sender, System.IO.FileSystemEventArgs e)
    {
        HandleEvent(e.FullPath, FileWatcherChange.Deleted, watchedFolder: GetWatchedFolder(e.FullPath));
    }

    private void OnFileRenamed(object sender, System.IO.RenamedEventArgs e)
    {
        HandleEvent(e.FullPath, FileWatcherChange.Renamed, e.OldFullPath, GetWatchedFolder(e.FullPath));
    }

    private void OnError(object sender, System.IO.ErrorEventArgs e)
    {
        var exception = e.GetException();
        _logger.LogError(exception, "FileSystemWatcher error");

        if (sender is System.IO.FileSystemWatcher watcher)
        {
            OverflowOccurred?.Invoke(this, watcher);
        }
    }

    private void HandleEvent(
        string filePath,
        FileWatcherChange changeType,
        string? oldFilePath = null,
        string? watchedFolder = null)
    {
        if (!IsSupportedFile(filePath) && !IsDirectory(filePath))
            return;

        if (IsIgnoredPath(filePath))
            return;

        var effectiveWatchedFolder = watchedFolder ?? GetWatchedFolder(filePath);
        if (effectiveWatchedFolder is null)
            return;

        if (IsThumbnailCachePath(filePath))
            return;

        if (IsTempFile(filePath))
            return;

        if (IsDebounced(filePath))
            return;

        var args = new FileWatcherEventArgs
        {
            FilePath = filePath,
            ChangeType = changeType,
            OldFilePath = oldFilePath,
            WatchedFolderPath = effectiveWatchedFolder
        };

        lock (_batchLock)
        {
            _pendingBatch.Add(args);
        }

        _debounceTimer.Change(BatchFlushMs, Timeout.Infinite);
    }

    private bool IsDebounced(string filePath)
    {
        var now = DateTime.UtcNow;
        if (_lastEventTimes.TryGetValue(filePath, out var lastTime))
        {
            if ((now - lastTime).TotalMilliseconds < DebounceMs)
                return true;
        }

        _lastEventTimes[filePath] = now;

        if (_lastEventTimes.Count > 10000)
        {
            var cutoff = now.AddSeconds(-5);
            var staleKeys = _lastEventTimes
                .Where(kvp => kvp.Value < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in staleKeys)
            {
                _lastEventTimes.TryRemove(key, out _);
            }
        }

        return false;
    }

    private void FlushBatch(object? state)
    {
        List<FileWatcherEventArgs> batch;

        lock (_batchLock)
        {
            batch = [.. _pendingBatch];
            _pendingBatch.Clear();
        }

        foreach (var args in batch)
        {
            try
            {
                FileChanged?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error raising file changed event for: {FilePath}", args.FilePath);
            }
        }

        if (batch.Count > 0)
        {
            _logger.LogDebug("Flushed {Count} file events", batch.Count);
        }
    }

    private string? GetWatchedFolder(string filePath)
    {
        foreach (var kvp in _watchers)
        {
            if (filePath.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Key;
        }
        return null;
    }

    private static bool IsSupportedFile(string filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath);
        return !string.IsNullOrEmpty(extension) && SupportedExtensions.Contains(extension.ToLowerInvariant());
    }

    private static bool IsIgnoredPath(string filePath)
    {
        var fileName = System.IO.Path.GetFileName(filePath);
        return IgnoredPaths.Contains(fileName);
    }

    private static bool IsDirectory(string path)
    {
        try
        {
            return System.IO.Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsThumbnailCachePath(string filePath)
    {
        return filePath.Contains("\\Cache\\", StringComparison.OrdinalIgnoreCase)
            || filePath.Contains("/Cache/", StringComparison.OrdinalIgnoreCase)
            || filePath.Contains("\\.photothumb\\", StringComparison.OrdinalIgnoreCase)
            || filePath.Contains("/.photothumb/", StringComparison.OrdinalIgnoreCase)
            || filePath.Contains("\\.phtcache\\", StringComparison.OrdinalIgnoreCase)
            || filePath.Contains("/.phtcache/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTempFile(string filePath)
    {
        var fileName = System.IO.Path.GetFileName(filePath);
        return fileName.StartsWith("~$", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".temp", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith(".", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _debounceTimer.Dispose();
        StopWatchingAll();
    }
}
