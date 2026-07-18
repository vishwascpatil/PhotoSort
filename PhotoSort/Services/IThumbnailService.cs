namespace PhotoSort.Services;

public enum ThumbnailSize
{
    Small = 256,
    Medium = 512
}

public interface IThumbnailService : IDisposable
{
    string GetCacheDirectory();
    string GetThumbnailPath(int photoId, ThumbnailSize size);
    bool ThumbnailExists(int photoId, ThumbnailSize size);
    bool IsStale(int photoId, ThumbnailSize size, DateTime sourceModifiedUtc);
    Task<string?> GenerateThumbnailAsync(string sourceFilePath, int photoId, ThumbnailSize size, CancellationToken cancellationToken = default);
    Task<string?> GetOrGenerateAsync(int photoId, string sourceFilePath, DateTime sourceModifiedUtc, ThumbnailSize size, CancellationToken cancellationToken = default);
    void DeleteThumbnail(int photoId, ThumbnailSize size);
    void DeleteAllThumbnails(int photoId);
    long GetCacheSizeBytes();
    int GetCachedCount();
}
