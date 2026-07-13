using PhotoSort.Models;

namespace PhotoSort.Services;

public interface IVideoThumbnailService : IDisposable
{
    bool IsInitialized { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<VideoThumbnailInformation?> GenerateThumbnailsAsync(
        int photoId, string filePath, CancellationToken cancellationToken = default);
    Task<VideoThumbnailInformation?> GetOrGenerateAsync(
        int photoId, string filePath, DateTime sourceModifiedUtc,
        CancellationToken cancellationToken = default);
    string GetPreviewClipPath(int photoId);
    Task<string?> GeneratePreviewClipAsync(
        int photoId, string filePath, CancellationToken cancellationToken = default);
    bool ThumbnailsExist(int photoId);
    bool IsStale(int photoId, DateTime sourceModifiedUtc);
    void DeleteThumbnails(int photoId);
    string GetThumbnailPath(int photoId, VideoThumbnailSize size);
    long GetCacheSizeBytes();
    int GetCachedCount();
}

public enum VideoThumbnailSize
{
    Small = 256,
    Medium = 512,
    Large = 1024
}
