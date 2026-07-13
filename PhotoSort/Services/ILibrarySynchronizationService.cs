using PhotoSort.Models;

namespace PhotoSort.Services;

public interface ILibrarySynchronizationService : IDisposable
{
    bool IsSynchronizing { get; }

    bool IsWatching { get; }

    SyncProgress GetProgress();

    event EventHandler<SyncProgress>? ProgressChanged;

    event EventHandler<SyncCompletedEventArgs>? SynchronizationCompleted;

    Task StartWatchingAsync(CancellationToken cancellationToken = default);

    Task StopWatchingAsync();

    Task SynchronizeAllAsync(CancellationToken cancellationToken = default);

    Task SynchronizeFolderAsync(string folderPath, CancellationToken cancellationToken = default);

    void PauseSync();

    void ResumeSync();

    void CancelSync();

    Task<SyncResult> ProcessPendingChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class SyncCompletedEventArgs : EventArgs
{
    public int FilesAdded { get; init; }

    public int FilesDeleted { get; init; }

    public int FilesModified { get; init; }

    public int FilesRenamed { get; init; }

    public TimeSpan Duration { get; init; }

    public bool IsPartial { get; init; }
}

public sealed class SyncResult
{
    public int FilesProcessed { get; set; }

    public int FilesAdded { get; set; }

    public int FilesDeleted { get; set; }

    public int FilesModified { get; set; }

    public int FilesRenamed { get; set; }

    public int FoldersAdded { get; set; }

    public int FoldersDeleted { get; set; }

    public int ErrorsEncountered { get; set; }

    public TimeSpan Duration { get; set; }

    public List<string> ErrorMessages { get; set; } = [];
}
