using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;

namespace PhotoSort.Services;

public sealed class MediaLoaderService : IMediaLoaderService
{
    private readonly ILogger<MediaLoaderService> _logger;
    private readonly ConcurrentDictionary<int, BitmapImage> _cache = new();
    private bool _disposed;

    private static readonly HashSet<string> ImageExtensions =
    [
        ".jpg", ".jpeg", ".png", ".heic", ".webp", ".bmp", ".gif", ".tiff", ".tif"
    ];

    private static readonly HashSet<string> VideoExtensions =
    [
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".webm", ".m4v"
    ];

    public MediaLoaderService(ILogger<MediaLoaderService> logger)
    {
        _logger = logger;
    }

    public BitmapImage? GetCached(int photoId)
    {
        return _cache.TryGetValue(photoId, out var image) ? image : null;
    }

    public async Task<BitmapImage?> LoadImageAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return null;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!ImageExtensions.Contains(extension))
            return null;

        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var bitmap = await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            });

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

    public void Preload(int photoId, string filePath)
    {
        if (_cache.ContainsKey(photoId))
            return;

        if (!File.Exists(filePath))
            return;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!ImageExtensions.Contains(extension))
            return;

        _ = LoadAndCacheAsync(photoId, filePath);
    }

    public void PreloadRange(IReadOnlyList<(int Id, string FilePath)> items)
    {
        foreach (var (id, filePath) in items)
        {
            Preload(id, filePath);
        }
    }

    public void EvictOutside(int centerId, int radius = 1)
    {
        var keysToRemove = _cache.Keys
            .Where(k => Math.Abs(k - centerId) > radius)
            .ToList();

        foreach (var key in keysToRemove)
        {
            if (_cache.TryRemove(key, out var image))
            {
                image.StreamSource?.Dispose();
            }
        }
    }

    public void EvictAll()
    {
        foreach (var kvp in _cache)
        {
            kvp.Value.StreamSource?.Dispose();
        }
        _cache.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        EvictAll();
    }

    private async Task LoadAndCacheAsync(int photoId, string filePath)
    {
        try
        {
            var image = await LoadImageAsync(filePath);
            if (image is not null)
            {
                _cache[photoId] = image;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Preload failed for photo {PhotoId}", photoId);
        }
    }
}
