using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;

namespace PhotoSort.Services;

public sealed class MediaLoaderService : IMediaLoaderService
{
    private readonly ILogger<MediaLoaderService> _logger;
    private readonly LruCache<int, BitmapImage> _cache;
    private bool _disposed;

    private const int CacheCapacity = 7;
    private const int MaxDecodeWidth = 3840;

    private static readonly HashSet<string> ImageExtensions =
    [
        ".jpg", ".jpeg", ".png", ".heic", ".webp", ".bmp", ".gif", ".tiff", ".tif"
    ];

    public int CacheCount => _cache.Count;

    public MediaLoaderService(ILogger<MediaLoaderService> logger)
    {
        _logger = logger;
        _cache = new LruCache<int, BitmapImage>(CacheCapacity);
    }

    public BitmapImage? GetCached(int photoId)
    {
        return _cache.TryGet(photoId, out var image) ? image : null;
    }

    public async Task<BitmapImage?> LoadImageAsync(
        int photoId, string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return null;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!ImageExtensions.Contains(extension))
            return null;

        if (_cache.TryGet(photoId, out var cached))
            return cached;

        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var bitmap = await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes);

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.None;
                bmp.DecodePixelWidth = MaxDecodeWidth;
                bmp.StreamSource = stream;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }, cancellationToken);

            _cache.Put(photoId, bitmap);
            return bitmap;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to load image: {FilePath}", filePath);
            return null;
        }
    }

    public void EvictOutside(int centerId, int radius)
    {
        var keys = _cache.GetKeys();
        foreach (var key in keys)
        {
            if (Math.Abs(key - centerId) > radius)
            {
                _cache.Remove(key);
            }
        }
    }

    public void EvictAll()
    {
        _cache.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cache.Clear();
        _cache.Dispose();
    }
}
