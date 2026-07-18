using System.Windows.Media.Imaging;

namespace PhotoSort.Services;

public interface IMediaLoaderService : IDisposable
{
    BitmapImage? GetCached(int photoId);
    Task<BitmapImage?> LoadImageAsync(int photoId, string filePath, CancellationToken cancellationToken = default);
    void EvictOutside(int centerId, int radius);
    void EvictAll();
    int CacheCount { get; }
}
