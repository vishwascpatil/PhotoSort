using PhotoSort.Models;

namespace PhotoSort.Services;

public interface IVideoPreviewCacheService : IDisposable
{
    Task<VideoPreviewStrip?> GetPreviewStripAsync(
        int photoId, string filePath, CancellationToken cancellationToken = default);
    Task<VideoPreviewStrip?> GeneratePreviewStripAsync(
        int photoId, string filePath, int frameCount = 5,
        CancellationToken cancellationToken = default);
    bool PreviewStripExists(int photoId);
    void DeletePreviewStrip(int photoId);
    string GetPreviewFramePath(int photoId, int frameIndex);
    long GetCacheSizeBytes();
}
