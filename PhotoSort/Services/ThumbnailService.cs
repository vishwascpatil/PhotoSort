using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.Extensions.Logging;

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

    public async Task<Bitmap?> GenerateThumbnailAsync(
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

        try
        {
            Bitmap? sourceImage;

            if (VideoThumbnailExtractor.IsVideoFile(sourceFilePath))
            {
                var targetSize = (int)size;
                sourceImage = VideoThumbnailExtractor.ExtractThumbnail(sourceFilePath, targetSize);
                if (sourceImage is null)
                    return null;
            }
            else
            {
                var bytes = await File.ReadAllBytesAsync(sourceFilePath, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                var sourceStream = new MemoryStream(bytes);
                sourceImage = Image.FromStream(sourceStream, useEmbeddedColorManagement: false, validateImageData: false) as Bitmap;
                if (sourceImage is null)
                    return null;
            }

            var bitmap = ResizeImage(sourceImage, (int)size, (int)size);
            sourceImage.Dispose();

            var outputPath = GetThumbnailPath(photoId, size);
            bitmap.Save(outputPath, ImageFormat.Jpeg);

            var result = new Bitmap(bitmap);
            bitmap.Dispose();

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutOfMemoryException)
        {
            _logger.LogWarning("Out of memory generating thumbnail for {FilePath}", sourceFilePath);
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

        var bitmap = await GenerateThumbnailAsync(sourceFilePath, photoId, size, cancellationToken);
        if (bitmap is null)
            return null;

        bitmap.Dispose();
        return path;
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

    private static Bitmap ResizeImage(Image source, int maxWidth, int maxHeight)
    {
        var ratioX = (double)maxWidth / source.Width;
        var ratioY = (double)maxHeight / source.Height;
        var ratio = Math.Min(ratioX, ratioY);

        var newWidth = (int)(source.Width * ratio);
        var newHeight = (int)(source.Height * ratio);

        if (newWidth <= 0) newWidth = 1;
        if (newHeight <= 0) newHeight = 1;

        var bitmap = new Bitmap(newWidth, newHeight);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;

        graphics.DrawImage(source, 0, 0, newWidth, newHeight);

        return bitmap;
    }
}
