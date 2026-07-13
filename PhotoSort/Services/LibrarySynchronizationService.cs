using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhotoSort.Data;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class LibrarySynchronizationService : ILibrarySynchronizationService
{
    private readonly IFileWatcherService _fileWatcherService;
    private readonly IPhotoRepository _photoRepository;
    private readonly IFolderRepository _folderRepository;
    private readonly IThumbnailCacheService _thumbnailCacheService;
    private readonly IMetadataExtractionService _metadataExtractionService;
    private readonly IDbContextFactory<PhotoSortDbContext> _contextFactory;
    private readonly ILogger<LibrarySynchronizationService> _logger;

    private readonly Channel<FileSystemEvent> _eventChannel;
    private readonly CancellationTokenSource _cts;
    private readonly ManualResetEventSlim _pauseEvent;
    private readonly object _progressLock;
    private readonly Timer _queueTimer;

    private SyncProgress _progress;
    private Task? _processingTask;
    private Task? _watcherTask;
    private bool _disposed;
    private Stopwatch? _syncStopwatch;

    private const int ChannelCapacity = 50000;
    private const int BatchSize = 100;
    private const int QueueCheckIntervalMs = 2000;

    public bool IsSynchronizing => _processingTask is { IsCompleted: false };

    public bool IsWatching => _fileWatcherService.IsWatching;

    public event EventHandler<SyncProgress>? ProgressChanged;
    public event EventHandler<SyncCompletedEventArgs>? SynchronizationCompleted;

    public LibrarySynchronizationService(
        IFileWatcherService fileWatcherService,
        IPhotoRepository photoRepository,
        IFolderRepository folderRepository,
        IThumbnailCacheService thumbnailCacheService,
        IMetadataExtractionService metadataExtractionService,
        IDbContextFactory<PhotoSortDbContext> contextFactory,
        ILogger<LibrarySynchronizationService> logger)
    {
        _fileWatcherService = fileWatcherService;
        _photoRepository = photoRepository;
        _folderRepository = folderRepository;
        _thumbnailCacheService = thumbnailCacheService;
        _metadataExtractionService = metadataExtractionService;
        _contextFactory = contextFactory;
        _logger = logger;

        _eventChannel = Channel.CreateBounded<FileSystemEvent>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        _cts = new CancellationTokenSource();
        _pauseEvent = new ManualResetEventSlim(true);
        _progressLock = new object();

        _progress = new SyncProgress
        {
            Phase = SyncPhase.Idle,
            IsWatcherHealthy = true
        };

        _queueTimer = new Timer(CheckQueueStatus, null, Timeout.Infinite, QueueCheckIntervalMs);

        _fileWatcherService.FileChanged += OnFileWatcherChanged;
        _fileWatcherService.OverflowOccurred += OnWatcherOverflow;
    }

    public SyncProgress GetProgress()
    {
        lock (_progressLock)
        {
            return _progress;
        }
    }

    public async Task StartWatchingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var folders = await context.Folders
                .AsNoTracking()
                .Select(f => f.FolderPath)
                .ToListAsync(cancellationToken);

            if (folders.Count > 0)
            {
                _fileWatcherService.StartWatching(folders);
            }

            _queueTimer.Change(0, QueueCheckIntervalMs);

            _watcherTask = Task.Run(() => ProcessEventsAsync(cancellationToken), cancellationToken);

            lock (_progressLock)
            {
                _progress.ActiveWatcherCount = _fileWatcherService.WatchedFolderCount;
                _progress.IsWatcherHealthy = true;
                _progress.Phase = SyncPhase.Idle;
                FireProgressChanged();
            }

            _logger.LogInformation(
                "Started watching {Count} folders for file changes",
                _fileWatcherService.WatchedFolderCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start watching folders");
            throw;
        }
    }

    public async Task StopWatchingAsync()
    {
        _fileWatcherService.StopWatchingAll();
        _queueTimer.Change(Timeout.Infinite, Timeout.Infinite);

        lock (_progressLock)
        {
            _progress.ActiveWatcherCount = 0;
            FireProgressChanged();
        }

        _logger.LogInformation("Stopped watching all folders");
        await Task.CompletedTask;
    }

    public async Task SynchronizeAllAsync(CancellationToken cancellationToken = default)
    {
        if (IsSynchronizing)
            return;

        _syncStopwatch = Stopwatch.StartNew();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        _processingTask = Task.Run(() => RunSynchronizationAsync(linkedCts.Token), linkedCts.Token);

        try
        {
            await _processingTask;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Synchronization failed");
        }
    }

    public async Task SynchronizeFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (IsSynchronizing)
            return;

        _syncStopwatch = Stopwatch.StartNew();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        _processingTask = Task.Run(() => RunFolderSynchronizationAsync(folderPath, linkedCts.Token), linkedCts.Token);

        try
        {
            await _processingTask;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Folder synchronization failed for {FolderPath}", folderPath);
        }
    }

    public void PauseSync()
    {
        if (IsSynchronizing)
        {
            _pauseEvent.Reset();
            UpdatePhase(SyncPhase.Paused);
        }
    }

    public void ResumeSync()
    {
        if (!IsSynchronizing)
            return;

        _pauseEvent.Set();

        lock (_progressLock)
        {
            if (_progress.Phase == SyncPhase.Paused)
            {
                _progress.Phase = SyncPhase.Processing;
                FireProgressChanged();
            }
        }
    }

    public void CancelSync()
    {
        _pauseEvent.Set();
        _cts.Cancel();
    }

    public async Task<SyncResult> ProcessPendingChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResult();
        var sw = Stopwatch.StartNew();

        try
        {
            await foreach (var batch in ReadEventBatchesAsync(cancellationToken))
            {
                _pauseEvent.Wait(cancellationToken);

                var batchResult = await ProcessBatchAsync(batch, cancellationToken);

                result.FilesAdded += batchResult.FilesAdded;
                result.FilesDeleted += batchResult.FilesDeleted;
                result.FilesModified += batchResult.FilesModified;
                result.FilesRenamed += batchResult.FilesRenamed;
                result.ErrorsEncountered += batchResult.ErrorsEncountered;
                result.ErrorMessages.AddRange(batchResult.ErrorMessages);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process pending changes");
            result.ErrorsEncountered++;
            result.ErrorMessages.Add(ex.Message);
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        result.FilesProcessed = result.FilesAdded + result.FilesDeleted + result.FilesModified + result.FilesRenamed;

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _queueTimer.Dispose();
        _cts.Cancel();
        _cts.Dispose();
        _pauseEvent.Dispose();
        _fileWatcherService.FileChanged -= OnFileWatcherChanged;
        _fileWatcherService.OverflowOccurred -= OnWatcherOverflow;
    }

    private async Task RunSynchronizationAsync(CancellationToken cancellationToken)
    {
        try
        {
            UpdatePhase(SyncPhase.Scanning);
            UpdateCurrentOperation("Scanning folders for changes...");

            await ScanAllFoldersAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            UpdatePhase(SyncPhase.Processing);

            var result = await ProcessPendingChangesAsync(cancellationToken);

            _syncStopwatch?.Stop();

            UpdatePhase(SyncPhase.Completed);

            _progress.LastSyncTime = DateTime.UtcNow;
            _progress.TotalProcessed = result.FilesProcessed;
            FireProgressChanged();

            SynchronizationCompleted?.Invoke(this, new SyncCompletedEventArgs
            {
                FilesAdded = result.FilesAdded,
                FilesDeleted = result.FilesDeleted,
                FilesModified = result.FilesModified,
                FilesRenamed = result.FilesRenamed,
                Duration = _syncStopwatch?.Elapsed ?? TimeSpan.Zero
            });

            _logger.LogInformation(
                "Synchronization completed: +{Added} -{Deleted} ~{Modified} ={Renamed} in {Duration}",
                result.FilesAdded, result.FilesDeleted, result.FilesModified, result.FilesRenamed,
                _syncStopwatch?.Elapsed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Synchronization was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Synchronization failed");
            UpdatePhase(SyncPhase.Error);
        }
    }

    private async Task RunFolderSynchronizationAsync(string folderPath, CancellationToken cancellationToken)
    {
        try
        {
            UpdatePhase(SyncPhase.Scanning);
            UpdateCurrentOperation($"Scanning {folderPath}...");

            await ScanFolderAsync(folderPath, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            UpdatePhase(SyncPhase.Processing);

            var result = await ProcessPendingChangesAsync(cancellationToken);

            _syncStopwatch?.Stop();

            UpdatePhase(SyncPhase.Completed);

            _progress.LastSyncTime = DateTime.UtcNow;
            _progress.TotalProcessed = result.FilesProcessed;
            FireProgressChanged();

            SynchronizationCompleted?.Invoke(this, new SyncCompletedEventArgs
            {
                FilesAdded = result.FilesAdded,
                FilesDeleted = result.FilesDeleted,
                FilesModified = result.FilesModified,
                FilesRenamed = result.FilesRenamed,
                Duration = _syncStopwatch?.Elapsed ?? TimeSpan.Zero
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Folder synchronization was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Folder synchronization failed for {FolderPath}", folderPath);
            UpdatePhase(SyncPhase.Error);
        }
    }

    private void OnFileWatcherChanged(object? sender, FileWatcherEventArgs e)
    {
        var fileEvent = new FileSystemEvent
        {
            FilePath = e.FilePath,
            ChangeType = e.ChangeType switch
            {
                FileWatcherChange.Created => FileSystemChange.Created,
                FileWatcherChange.Modified => FileSystemChange.Modified,
                FileWatcherChange.Deleted => FileSystemChange.Deleted,
                FileWatcherChange.Renamed => FileSystemChange.Renamed,
                _ => FileSystemChange.Modified
            },
            OldFilePath = e.OldFilePath,
            WatchedFolderPath = e.WatchedFolderPath
        };

        if (!_eventChannel.Writer.TryWrite(fileEvent))
        {
            _logger.LogWarning("Event channel is full, dropping event for: {FilePath}", e.FilePath);
        }
    }

    private void OnWatcherOverflow(object? sender, System.IO.FileSystemWatcher watcher)
    {
        lock (_progressLock)
        {
            _progress.WatcherOverflowCount++;
            _progress.IsWatcherHealthy = false;
            FireProgressChanged();
        }

        _logger.LogWarning(
            "FileSystemWatcher overflow detected in folder: {Path}. " +
            "Consider reducing the number of files or increasing buffer size.",
            watcher.Path);
    }

    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var batch in ReadEventBatchesAsync(cancellationToken))
            {
                _pauseEvent.Wait(cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                    break;

                await ProcessBatchAsync(batch, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Event processing loop failed");
        }
    }

    private async IAsyncEnumerable<List<FileSystemEvent>> ReadEventBatchesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var batch = new List<FileSystemEvent>(BatchSize);

        await foreach (var evt in _eventChannel.Reader.ReadAllAsync(cancellationToken))
        {
            batch.Add(evt);

            if (batch.Count >= BatchSize)
            {
                yield return batch;
                batch = new List<FileSystemEvent>(BatchSize);
            }
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    private async Task<SyncResult> ProcessBatchAsync(
        List<FileSystemEvent> batch,
        CancellationToken cancellationToken)
    {
        var result = new SyncResult();

        var created = batch.Where(e => e.ChangeType == FileSystemChange.Created).ToList();
        var deleted = batch.Where(e => e.ChangeType == FileSystemChange.Deleted).ToList();
        var modified = batch.Where(e => e.ChangeType == FileSystemChange.Modified).ToList();
        var renamed = batch.Where(e => e.ChangeType == FileSystemChange.Renamed).ToList();

        foreach (var evt in created)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _pauseEvent.Wait(cancellationToken);

            try
            {
                await HandleCreatedAsync(evt, cancellationToken);
                result.FilesAdded++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle created file: {FilePath}", evt.FilePath);
                result.ErrorsEncountered++;
                result.ErrorMessages.Add($"Created {evt.FilePath}: {ex.Message}");
            }
        }

        foreach (var evt in deleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _pauseEvent.Wait(cancellationToken);

            try
            {
                await HandleDeletedAsync(evt, cancellationToken);
                result.FilesDeleted++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle deleted file: {FilePath}", evt.FilePath);
                result.ErrorsEncountered++;
                result.ErrorMessages.Add($"Deleted {evt.FilePath}: {ex.Message}");
            }
        }

        foreach (var evt in modified)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _pauseEvent.Wait(cancellationToken);

            try
            {
                await HandleModifiedAsync(evt, cancellationToken);
                result.FilesModified++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle modified file: {FilePath}", evt.FilePath);
                result.ErrorsEncountered++;
                result.ErrorMessages.Add($"Modified {evt.FilePath}: {ex.Message}");
            }
        }

        foreach (var evt in renamed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _pauseEvent.Wait(cancellationToken);

            try
            {
                await HandleRenamedAsync(evt, cancellationToken);
                result.FilesRenamed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle renamed file: {FilePath}", evt.FilePath);
                result.ErrorsEncountered++;
                result.ErrorMessages.Add($"Renamed {evt.FilePath}: {ex.Message}");
            }
        }

        UpdateProgressCounts(result);
        UpdateCurrentOperation($"Processed batch of {batch.Count} events");

        return result;
    }

    private async Task HandleCreatedAsync(FileSystemEvent evt, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await context.Photos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.FilePath == evt.FilePath, cancellationToken);

        if (existing is not null)
            return;

        var folder = await GetOrCreateFolderAsync(context, evt.WatchedFolderPath, cancellationToken);

        var fileInfo = new System.IO.FileInfo(evt.FilePath);
        if (!fileInfo.Exists)
            return;

        var photo = new Photo
        {
            FilePath = evt.FilePath,
            FileName = System.IO.Path.GetFileName(evt.FilePath),
            Extension = System.IO.Path.GetExtension(evt.FilePath),
            FileSize = fileInfo.Length,
            ModifiedDateUtc = fileInfo.LastWriteTimeUtc,
            FolderId = folder.Id,
            State = ProcessingState.NotIndexed
        };

        context.Photos.Add(photo);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Added new photo to database: {FilePath}", evt.FilePath);
    }

    private async Task HandleDeletedAsync(FileSystemEvent evt, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var photo = await context.Photos
            .FirstOrDefaultAsync(p => p.FilePath == evt.FilePath, cancellationToken);

        if (photo is null)
            return;

        if (!string.IsNullOrEmpty(photo.ThumbnailSmallPath) && System.IO.File.Exists(photo.ThumbnailSmallPath))
        {
            try { System.IO.File.Delete(photo.ThumbnailSmallPath); } catch { }
        }

        if (!string.IsNullOrEmpty(photo.ThumbnailMediumPath) && System.IO.File.Exists(photo.ThumbnailMediumPath))
        {
            try { System.IO.File.Delete(photo.ThumbnailMediumPath); } catch { }
        }

        if (!string.IsNullOrEmpty(photo.VideoThumbnailSmallPath) && System.IO.File.Exists(photo.VideoThumbnailSmallPath))
        {
            try { System.IO.File.Delete(photo.VideoThumbnailSmallPath); } catch { }
        }

        if (!string.IsNullOrEmpty(photo.VideoThumbnailMediumPath) && System.IO.File.Exists(photo.VideoThumbnailMediumPath))
        {
            try { System.IO.File.Delete(photo.VideoThumbnailMediumPath); } catch { }
        }

        if (!string.IsNullOrEmpty(photo.VideoThumbnailLargePath) && System.IO.File.Exists(photo.VideoThumbnailLargePath))
        {
            try { System.IO.File.Delete(photo.VideoThumbnailLargePath); } catch { }
        }

        context.Photos.Remove(photo);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Removed photo from database: {FilePath}", evt.FilePath);
    }

    private async Task HandleModifiedAsync(FileSystemEvent evt, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var photo = await context.Photos
            .FirstOrDefaultAsync(p => p.FilePath == evt.FilePath, cancellationToken);

        if (photo is null)
        {
            await HandleCreatedAsync(evt, cancellationToken);
            return;
        }

        var fileInfo = new System.IO.FileInfo(evt.FilePath);
        if (!fileInfo.Exists)
            return;

        if (Math.Abs((fileInfo.LastWriteTimeUtc - photo.ModifiedDateUtc).TotalSeconds) < 1
            && fileInfo.Length == photo.FileSize)
        {
            return;
        }

        var oldVideoSmallPath = photo.VideoThumbnailSmallPath;
        var oldVideoMediumPath = photo.VideoThumbnailMediumPath;
        var oldVideoLargePath = photo.VideoThumbnailLargePath;

        photo.FileSize = fileInfo.Length;
        photo.ModifiedDateUtc = fileInfo.LastWriteTimeUtc;
        photo.State = ProcessingState.Indexed;
        photo.MetadataExtractedDate = null;
        photo.ThumbnailGeneratedDate = null;
        photo.VideoThumbnailSmallPath = null;
        photo.VideoThumbnailMediumPath = null;
        photo.VideoThumbnailLargePath = null;
        photo.VideoThumbnailDate = null;
        photo.VideoThumbnailVersion = 0;

        await context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(photo.ThumbnailSmallPath) && System.IO.File.Exists(photo.ThumbnailSmallPath))
        {
            try { System.IO.File.Delete(photo.ThumbnailSmallPath); } catch { }
        }

        if (!string.IsNullOrEmpty(photo.ThumbnailMediumPath) && System.IO.File.Exists(photo.ThumbnailMediumPath))
        {
            try { System.IO.File.Delete(photo.ThumbnailMediumPath); } catch { }
        }

        if (!string.IsNullOrEmpty(oldVideoSmallPath) && System.IO.File.Exists(oldVideoSmallPath))
        {
            try { System.IO.File.Delete(oldVideoSmallPath); } catch { }
        }

        if (!string.IsNullOrEmpty(oldVideoMediumPath) && System.IO.File.Exists(oldVideoMediumPath))
        {
            try { System.IO.File.Delete(oldVideoMediumPath); } catch { }
        }

        if (!string.IsNullOrEmpty(oldVideoLargePath) && System.IO.File.Exists(oldVideoLargePath))
        {
            try { System.IO.File.Delete(oldVideoLargePath); } catch { }
        }

        _logger.LogDebug("Updated photo metadata: {FilePath}", evt.FilePath);
    }

    private async Task HandleRenamedAsync(FileSystemEvent evt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(evt.OldFilePath))
        {
            await HandleCreatedAsync(evt, cancellationToken);
            return;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var photo = await context.Photos
            .FirstOrDefaultAsync(p => p.FilePath == evt.OldFilePath, cancellationToken);

        if (photo is null)
        {
            await HandleCreatedAsync(evt, cancellationToken);
            return;
        }

        var fileInfo = new System.IO.FileInfo(evt.FilePath);
        if (!fileInfo.Exists)
            return;

        photo.FilePath = evt.FilePath;
        photo.FileName = System.IO.Path.GetFileName(evt.FilePath);
        photo.Extension = System.IO.Path.GetExtension(evt.FilePath);
        photo.FileSize = fileInfo.Length;
        photo.ModifiedDateUtc = fileInfo.LastWriteTimeUtc;

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Renamed photo: {OldPath} -> {NewPath}", evt.OldFilePath, evt.FilePath);
    }

    private async Task ScanAllFoldersAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var folders = await context.Folders
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var folder in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ScanFolderAsync(folder.FolderPath, cancellationToken);
        }
    }

    private async Task ScanFolderAsync(string folderPath, CancellationToken cancellationToken)
    {
        if (!System.IO.Directory.Exists(folderPath))
        {
            _logger.LogWarning("Folder no longer accessible: {FolderPath}", folderPath);
            return;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var folder = await context.Folders
            .FirstOrDefaultAsync(f => f.FolderPath == folderPath, cancellationToken);

        if (folder is null)
            return;

        var existingPhotos = await context.Photos
            .AsNoTracking()
            .Where(p => p.FolderId == folder.Id)
            .Select(p => p.FilePath)
            .ToHashSetAsync(cancellationToken);

        var currentFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(folderPath, "*.*", System.IO.SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var extension = System.IO.Path.GetExtension(file);
                if (string.IsNullOrEmpty(extension))
                    continue;

                currentFiles.Add(file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan folder: {FolderPath}", folderPath);
        }

        var deletedFiles = existingPhotos.Except(currentFiles, StringComparer.OrdinalIgnoreCase);

        foreach (var deletedFile in deletedFiles)
        {
            await _eventChannel.Writer.WriteAsync(new FileSystemEvent
            {
                FilePath = deletedFile,
                ChangeType = FileSystemChange.Deleted,
                WatchedFolderPath = folderPath
            }, cancellationToken);
        }

        folder.LastScanDate = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Folder> GetOrCreateFolderAsync(
        PhotoSortDbContext context,
        string folderPath,
        CancellationToken cancellationToken)
    {
        var existing = await context.Folders
            .FirstOrDefaultAsync(f => f.FolderPath == folderPath, cancellationToken);

        if (existing is not null)
            return existing;

        var folder = new Folder
        {
            FolderPath = folderPath,
            AddedDate = DateTime.UtcNow,
            LastScanDate = DateTime.UtcNow
        };

        context.Folders.Add(folder);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created new folder record: {FolderPath}", folderPath);

        return folder;
    }

    private void CheckQueueStatus(object? state)
    {
        lock (_progressLock)
        {
            _progress.QueueLength = _eventChannel.Reader.Count;
            _progress.ActiveWatcherCount = _fileWatcherService.WatchedFolderCount;
            FireProgressChanged();
        }
    }

    private void UpdateProgressCounts(SyncResult result)
    {
        lock (_progressLock)
        {
            _progress.FilesAdded += result.FilesAdded;
            _progress.FilesDeleted += result.FilesDeleted;
            _progress.FilesModified += result.FilesModified;
            _progress.FilesRenamed += result.FilesRenamed;
            _progress.TotalProcessed = _progress.FilesAdded + _progress.FilesDeleted
                + _progress.FilesModified + _progress.FilesRenamed;
            FireProgressChanged();
        }
    }

    private void UpdatePhase(SyncPhase phase)
    {
        lock (_progressLock)
        {
            _progress.Phase = phase;
            FireProgressChanged();
        }
    }

    private void UpdateCurrentOperation(string operation)
    {
        lock (_progressLock)
        {
            _progress.CurrentOperation = operation;
            FireProgressChanged();
        }
    }

    private void FireProgressChanged()
    {
        ProgressChanged?.Invoke(this, _progress);
    }
}
