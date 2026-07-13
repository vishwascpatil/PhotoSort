using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using PhotoSort.Models;

namespace PhotoSort.Services;

[SupportedOSPlatform("windows")]
public sealed class VideoPreviewCacheService : IVideoPreviewCacheService
{
    private readonly ILogger<VideoPreviewCacheService> _logger;
    private readonly string _previewDir;
    private bool _disposed;

    private const int PreviewFrameSize = 256;
    private const int DefaultFrameCount = 5;
    private const int CurrentPreviewVersion = 1;

    public VideoPreviewCacheService(ILogger<VideoPreviewCacheService> logger)
    {
        _logger = logger;

        _previewDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhotoSort", "Cache", "Video", "Preview");

        Directory.CreateDirectory(_previewDir);
    }

    public string GetPreviewFramePath(int photoId, int frameIndex)
    {
        return Path.Combine(_previewDir, $"{photoId}_frame{frameIndex}.jpg");
    }

    public bool PreviewStripExists(int photoId)
    {
        var firstFrame = GetPreviewFramePath(photoId, 0);
        return File.Exists(firstFrame);
    }

    public async Task<VideoPreviewStrip?> GetPreviewStripAsync(
        int photoId, string filePath, CancellationToken cancellationToken = default)
    {
        if (!PreviewStripExists(photoId))
            return await GeneratePreviewStripAsync(photoId, filePath, DefaultFrameCount, cancellationToken);

        var frames = new List<VideoPreviewFrame>();
        for (int i = 0; i < DefaultFrameCount; i++)
        {
            var path = GetPreviewFramePath(photoId, i);
            if (File.Exists(path))
            {
                frames.Add(new VideoPreviewFrame
                {
                    Index = i,
                    ImagePath = path,
                    Timestamp = 0
                });
            }
        }

        if (frames.Count == 0)
            return await GeneratePreviewStripAsync(photoId, filePath, DefaultFrameCount, cancellationToken);

        return new VideoPreviewStrip
        {
            PhotoId = photoId,
            FrameCount = frames.Count,
            Frames = frames,
            GeneratedDate = File.GetLastWriteTimeUtc(GetPreviewFramePath(photoId, 0)),
            Version = CurrentPreviewVersion
        };
    }

    public async Task<VideoPreviewStrip?> GeneratePreviewStripAsync(
        int photoId, string filePath, int frameCount = 5,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath) || !VideoThumbnailExtractor.IsVideoFile(filePath))
            return null;

        try
        {
            var duration = VideoThumbnailExtractor.GetDuration(filePath);
            if (duration <= 0) return null;

            var frames = new List<VideoPreviewFrame>();
            var skipStart = duration * 0.05;
            var skipEnd = duration * 0.95;
            var usableDuration = skipEnd - skipStart;

            for (int i = 0; i < frameCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var timestamp = skipStart + usableDuration * (i + 1) / (frameCount + 1);
                var frame = VideoThumbnailExtractor.ExtractFrameAtTime(filePath, timestamp);

                if (frame is null) continue;

                var path = GetPreviewFramePath(photoId, i);
                var resized = ResizeForPreview(frame, PreviewFrameSize);
                frame.Dispose();

                resized.Save(path, ImageFormat.Jpeg);
                resized.Dispose();

                frames.Add(new VideoPreviewFrame
                {
                    Index = i,
                    Timestamp = timestamp,
                    ImagePath = path,
                    Score = 1.0
                });
            }

            if (frames.Count == 0) return null;

            _logger.LogDebug("Generated {Count} preview frames for {PhotoId}", frames.Count, photoId);

            return await Task.FromResult(new VideoPreviewStrip
            {
                PhotoId = photoId,
                FrameCount = frames.Count,
                Frames = frames,
                GeneratedDate = DateTime.UtcNow,
                Version = CurrentPreviewVersion
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate preview strip for {FilePath}", filePath);
            return null;
        }
    }

    public void DeletePreviewStrip(int photoId)
    {
        for (int i = 0; i < 20; i++)
        {
            var path = GetPreviewFramePath(photoId, i);
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { /* ignored */ }
        }
    }

    public long GetCacheSizeBytes()
    {
        long total = 0;
        try
        {
            if (Directory.Exists(_previewDir))
            {
                foreach (var file in Directory.EnumerateFiles(_previewDir, "*.jpg"))
                    total += new FileInfo(file).Length;
            }
        }
        catch { /* ignored */ }
        return total;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    private static Bitmap ResizeForPreview(Bitmap source, int targetSize)
    {
        var ratioX = (double)targetSize / source.Width;
        var ratioY = (double)targetSize / source.Height;
        var ratio = Math.Min(ratioX, ratioY);

        var newWidth = Math.Max(1, (int)(source.Width * ratio));
        var newHeight = Math.Max(1, (int)(source.Height * ratio));

        var bitmap = new Bitmap(newWidth, newHeight);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, 0, 0, newWidth, newHeight);

        return bitmap;
    }
}
