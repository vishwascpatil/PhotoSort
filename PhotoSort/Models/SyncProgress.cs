namespace PhotoSort.Models;

public enum SyncPhase
{
    Idle,
    Scanning,
    Processing,
    Completed,
    Paused,
    Error
}

public sealed class SyncProgress
{
    public SyncPhase Phase { get; set; }

    public int FilesAdded { get; set; }

    public int FilesDeleted { get; set; }

    public int FilesModified { get; set; }

    public int FilesRenamed { get; set; }

    public int FoldersAdded { get; set; }

    public int FoldersDeleted { get; set; }

    public int QueueLength { get; set; }

    public int TotalProcessed { get; set; }

    public int TotalPending { get; set; }

    public string CurrentOperation { get; set; } = string.Empty;

    public string CurrentFile { get; set; } = string.Empty;

    public DateTime LastSyncTime { get; set; }

    public TimeSpan Elapsed { get; set; }

    public int ActiveWatcherCount { get; set; }

    public bool IsWatcherHealthy { get; set; } = true;

    public int WatcherOverflowCount { get; set; }

    public long ReclaimableBytes { get; set; }

    public double ProgressPercent => TotalPending > 0
        ? (double)TotalProcessed / TotalPending * 100
        : 0;

    public string DisplayReclaimable => FormatFileSize(ReclaimableBytes);

    public string PhaseDisplay => Phase switch
    {
        SyncPhase.Idle => "Idle - Watching for changes",
        SyncPhase.Scanning => "Scanning folders...",
        SyncPhase.Processing => "Processing changes...",
        SyncPhase.Completed => "Synchronized",
        SyncPhase.Paused => "Paused",
        SyncPhase.Error => "Error occurred",
        _ => "Unknown"
    };

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }
}
