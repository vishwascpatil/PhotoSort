namespace PhotoSort.Services;

public enum ThumbnailPriority
{
    High = 0,
    Medium = 1,
    Low = 2
}

public sealed class ThumbnailRequest
{
    public required int PhotoId { get; init; }
    public required string FilePath { get; init; }
    public required DateTime SourceModifiedUtc { get; init; }
    public ThumbnailPriority Priority { get; init; } = ThumbnailPriority.Low;
    public bool SmallOnly { get; init; }
}

public sealed class ThumbnailProgress
{
    public int QueuedCount { get; init; }
    public int GeneratedCount { get; init; }
    public int FailedCount { get; init; }
    public long CacheSizeBytes { get; init; }
    public double GenerationRatePerSecond { get; init; }
}

public interface IThumbnailCacheService : IDisposable
{
    event EventHandler<ThumbnailProgress>? ProgressChanged;
    event EventHandler<int>? ThumbnailReady;

    void Enqueue(int photoId, string filePath, DateTime sourceModifiedUtc, ThumbnailPriority priority, bool smallOnly = false);
    void EnqueueRange(IReadOnlyList<ThumbnailRequest> requests);
    void CancelAll();
    ThumbnailProgress GetProgress();
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}
