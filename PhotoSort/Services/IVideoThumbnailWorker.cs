using PhotoSort.Models;

namespace PhotoSort.Services;

public interface IVideoThumbnailWorker : IDisposable
{
    event EventHandler<int>? ThumbnailReady;
    event EventHandler<VideoThumbnailProgress>? ProgressChanged;

    void Enqueue(int photoId, string filePath, DateTime sourceModifiedUtc, ThumbnailPriority priority);
    void EnqueueRange(IReadOnlyList<(int PhotoId, string FilePath, DateTime SourceModifiedUtc)> items);
    void CancelAll();
    void Pause();
    void Resume();
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}
