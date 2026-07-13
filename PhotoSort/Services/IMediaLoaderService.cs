using System.Windows.Media.Imaging;

namespace PhotoSort.Services;

public interface IMediaLoaderService : IDisposable
{
    BitmapImage? GetCached(int photoId);

    Task<BitmapImage?> LoadImageAsync(string filePath, CancellationToken cancellationToken = default);

    void Preload(int photoId, string filePath);

    void PreloadRange(IReadOnlyList<(int Id, string FilePath)> items);

    void EvictOutside(int centerId, int radius = 1);

    void EvictAll();
}
