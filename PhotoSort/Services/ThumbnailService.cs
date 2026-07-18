using System.IO;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace PhotoSort.Services;

public sealed class ThumbnailService : IThumbnailService
{
    private readonly ILogger<ThumbnailService> _logger;
    private readonly string _cacheRoot;
    private readonly string _smallDir;
    private readonly string _mediumDir;
    private readonly HashSet<string> _imageExtensions;
    private bool _disposed;

    public ThumbnailService(ILogger<ThumbnailService> logger)
    {
        _logger = logger;

        _cacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhotoSort",
            "Cache");

        _smallDir = Path.Combine(_cacheRoot, "Small");
        _mediumDir = Path.Combine(_cacheRoot, "Medium");

        Directory.CreateDirectory(_smallDir);
        Directory.CreateDirectory(_mediumDir);

        _imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".heic", ".webp", ".bmp", ".gif", ".tiff", ".tif"
        };
    }

    public string GetCacheDirectory() => _cacheRoot;

    public string GetThumbnailPath(int photoId, ThumbnailSize size)
    {
        var dir = size == ThumbnailSize.Small ? _smallDir : _mediumDir;
        return Path.Combine(dir, $"{photoId}.jpg");
    }

    public bool ThumbnailExists(int photoId, ThumbnailSize size)
    {
        return File.Exists(GetThumbnailPath(photoId, size));
    }

    public bool IsStale(int photoId, ThumbnailSize size, DateTime sourceModifiedUtc)
    {
        var path = GetThumbnailPath(photoId, size);
        if (!File.Exists(path))
            return true;

        var fileInfo = new FileInfo(path);
        return fileInfo.LastWriteTimeUtc < sourceModifiedUtc;
    }

    public async Task<string?> GenerateThumbnailAsync(
        string sourceFilePath,
        int photoId,
        ThumbnailSize size,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceFilePath))
            return null;

        var extension = Path.GetExtension(sourceFilePath);
        if (!_imageExtensions.Contains(extension) && !VideoThumbnailExtractor.IsVideoFile(sourceFilePath))
            return null;

        var outputPath = GetThumbnailPath(photoId, size);

        try
        {
            if (VideoThumbnailExtractor.IsVideoFile(sourceFilePath))
            {
                var framePath = VideoThumbnailExtractor.ExtractThumbnail(sourceFilePath, (int)size);
                if (framePath is null)
                    return null;

                try
                {
                    using var image = await Image.LoadAsync(framePath, cancellationToken);
                    ResizeImage(image, (int)size);
                    var encoder = new JpegEncoder { Quality = 85 };
                    await image.SaveAsJpegAsync(outputPath, encoder, cancellationToken);
                }
                finally
                {
                    TryDelete(framePath);
                }
            }
            else
            {
                using var image = await Image.LoadAsync(sourceFilePath, cancellationToken);
                ResizeImage(image, (int)size);
                var encoder = new JpegEncoder { Quality = 85 };
                await image.SaveAsJpegAsync(outputPath, encoder, cancellationToken);
            }

            return outputPath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidImageContentException)
        {
            _logger.LogWarning("Invalid or corrupted image: {FilePath}", sourceFilePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to generate thumbnail for {FilePath}", sourceFilePath);
            return null;
        }
    }

    public async Task<string?> GetOrGenerateAsync(
        int photoId,
        string sourceFilePath,
        DateTime sourceModifiedUtc,
        ThumbnailSize size,
        CancellationToken cancellationToken = default)
    {
        var path = GetThumbnailPath(photoId, size);

        if (File.Exists(path) && !IsStale(photoId, size, sourceModifiedUtc))
            return path;

        return await GenerateThumbnailAsync(sourceFilePath, photoId, size, cancellationToken);
    }

    public void DeleteThumbnail(int photoId, ThumbnailSize size)
    {
        var path = GetThumbnailPath(photoId, size);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to delete thumbnail {Path}", path);
        }
    }

    public void DeleteAllThumbnails(int photoId)
    {
        DeleteThumbnail(photoId, ThumbnailSize.Small);
        DeleteThumbnail(photoId, ThumbnailSize.Medium);
    }

    public long GetCacheSizeBytes()
    {
        long total = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(_smallDir, "*.jpg"))
                total += new FileInfo(file).Length;

            foreach (var file in Directory.EnumerateFiles(_mediumDir, "*.jpg"))
                total += new FileInfo(file).Length;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to calculate cache size");
        }

        return total;
    }

    public int GetCachedCount()
    {
        int count = 0;

        try
        {
            count += Directory.EnumerateFiles(_smallDir, "*.jpg").Count();
        }
        catch { /* ignored */ }

        try
        {
            count += Directory.EnumerateFiles(_mediumDir, "*.jpg").Count();
        }
        catch { /* ignored */ }

        return count;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }

    private static void ResizeImage(Image image, int maxDimension)
    {
        var ratioX = (double)maxDimension / image.Width;
        var ratioY = (double)maxDimension / image.Height;
        var ratio = Math.Min(ratioX, ratioY);

        var newWidth = (int)(image.Width * ratio);
        var newHeight = (int)(image.Height * ratio);

        if (newWidth <= 0) newWidth = 1;
        if (newHeight <= 0) newHeight = 1;

        image.Mutate(x => x.Resize(newWidth, newHeight, KnownResamplers.Lanczos3));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { /* ignore cleanup failures */ }
    }
}
