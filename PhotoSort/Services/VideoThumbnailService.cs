using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using PhotoSort.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace PhotoSort.Services;

[SupportedOSPlatform("windows")]
public sealed class VideoThumbnailService : IVideoThumbnailService
{
    private readonly ILogger<VideoThumbnailService> _logger;
    private readonly string _cacheRoot;
    private readonly string _smallDir;
    private readonly string _mediumDir;
    private readonly string _largeDir;
    private bool _disposed;
    private bool _isInitialized;

    private const int CurrentThumbnailVersion = 1;

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".webm", ".m4v",
        ".mpg", ".mpeg", ".3gp", ".flv", ".ts", ".mts", ".m2ts"
    };

    public bool IsInitialized => _isInitialized;

    public VideoThumbnailService(ILogger<VideoThumbnailService> logger)
    {
        _logger = logger;

        _cacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhotoSort", "Cache", "Video");

        _smallDir = Path.Combine(_cacheRoot, "Small");
        _mediumDir = Path.Combine(_cacheRoot, "Medium");
        _largeDir = Path.Combine(_cacheRoot, "Large");

        Directory.CreateDirectory(_smallDir);
        Directory.CreateDirectory(_mediumDir);
        Directory.CreateDirectory(_largeDir);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        try
        {
            await FFmpegInitializer.EnsureFFmpegAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FFmpeg initialization failed");
        }

        _isInitialized = true;
    }

    public string GetThumbnailPath(int photoId, VideoThumbnailSize size)
    {
        var dir = size switch
        {
            VideoThumbnailSize.Small => _smallDir,
            VideoThumbnailSize.Medium => _mediumDir,
            _ => _largeDir
        };
        return Path.Combine(dir, $"{photoId}.jpg");
    }

    public bool ThumbnailsExist(int photoId)
    {
        return File.Exists(GetThumbnailPath(photoId, VideoThumbnailSize.Small))
            && File.Exists(GetThumbnailPath(photoId, VideoThumbnailSize.Medium));
    }

    public bool IsStale(int photoId, DateTime sourceModifiedUtc)
    {
        var path = GetThumbnailPath(photoId, VideoThumbnailSize.Small);
        if (!File.Exists(path))
            return true;

        var fileInfo = new FileInfo(path);
        return fileInfo.LastWriteTimeUtc < sourceModifiedUtc;
    }

    public async Task<VideoThumbnailInformation?> GenerateThumbnailsAsync(
        int photoId, string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return null;

        var extension = Path.GetExtension(filePath);
        if (!VideoExtensions.Contains(extension))
            return null;

        var sw = Stopwatch.StartNew();

        try
        {
            var bestFramePath = await ExtractBestFrameUsingThumbnailFilterAsync(filePath, cancellationToken);

            if (bestFramePath is null)
                bestFramePath = ExtractFallbackFrame(filePath);

            if (bestFramePath is null)
            {
                _logger.LogWarning("Failed to extract any frame from {FilePath}", filePath);
                return null;
            }

            try
            {
                using var bestImage = await Image.LoadAsync(bestFramePath, cancellationToken);

                var smallPath = GetThumbnailPath(photoId, VideoThumbnailSize.Small);
                var mediumPath = GetThumbnailPath(photoId, VideoThumbnailSize.Medium);
                var largePath = GetThumbnailPath(photoId, VideoThumbnailSize.Large);

                await SaveResizedAsync(bestImage, smallPath, 256, cancellationToken);
                await SaveResizedAsync(bestImage, mediumPath, 512, cancellationToken);
                await SaveResizedAsync(bestImage, largePath, 1024, cancellationToken);
            }
            finally
            {
                TryDelete(bestFramePath);
            }

            sw.Stop();

            var info = new VideoThumbnailInformation
            {
                PhotoId = photoId,
                FilePath = filePath,
                SmallPath = GetThumbnailPath(photoId, VideoThumbnailSize.Small),
                MediumPath = GetThumbnailPath(photoId, VideoThumbnailSize.Medium),
                LargePath = GetThumbnailPath(photoId, VideoThumbnailSize.Large),
                GeneratedDate = DateTime.UtcNow,
                GenerationTime = sw.Elapsed,
                Version = CurrentThumbnailVersion
            };

            _logger.LogDebug(
                "Generated video thumbnails for {PhotoId} in {Elapsed}ms",
                photoId, sw.ElapsedMilliseconds);

            return info;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate video thumbnails for {FilePath}", filePath);
            return null;
        }
    }

    public async Task<VideoThumbnailInformation?> GetOrGenerateAsync(
        int photoId, string filePath, DateTime sourceModifiedUtc,
        CancellationToken cancellationToken = default)
    {
        if (!IsStale(photoId, sourceModifiedUtc) && ThumbnailsExist(photoId))
        {
            return new VideoThumbnailInformation
            {
                PhotoId = photoId,
                FilePath = filePath,
                SmallPath = GetThumbnailPath(photoId, VideoThumbnailSize.Small),
                MediumPath = GetThumbnailPath(photoId, VideoThumbnailSize.Medium),
                LargePath = GetThumbnailPath(photoId, VideoThumbnailSize.Large),
                Version = CurrentThumbnailVersion
            };
        }

        return await GenerateThumbnailsAsync(photoId, filePath, cancellationToken);
    }

    public void DeleteThumbnails(int photoId)
    {
        foreach (var size in Enum.GetValues<VideoThumbnailSize>())
        {
            var path = GetThumbnailPath(photoId, size);
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to delete video thumbnail {Path}", path);
            }
        }
    }

    public long GetCacheSizeBytes()
    {
        long total = 0;
        try
        {
            foreach (var dir in new[] { _smallDir, _mediumDir, _largeDir })
            {
                if (Directory.Exists(dir))
                {
                    foreach (var file in Directory.EnumerateFiles(dir, "*.jpg"))
                        total += new FileInfo(file).Length;
                }
            }
        }
        catch { /* ignored */ }
        return total;
    }

    public int GetCachedCount()
    {
        int count = 0;
        try
        {
            if (Directory.Exists(_smallDir))
                count += Directory.EnumerateFiles(_smallDir, "*.jpg").Count();
        }
        catch { /* ignored */ }
        return count;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    private static async Task SaveResizedAsync(Image image, string outputPath, int targetSize, CancellationToken cancellationToken)
    {
        var ratioX = (double)targetSize / image.Width;
        var ratioY = (double)targetSize / image.Height;
        var ratio = Math.Min(ratioX, ratioY);

        var newWidth = Math.Max(1, (int)(image.Width * ratio));
        var newHeight = Math.Max(1, (int)(image.Height * ratio));

        using var clone = image.Clone(x => x.Resize(newWidth, newHeight, KnownResamplers.Lanczos3));
        var encoder = new JpegEncoder { Quality = 85 };
        await clone.SaveAsJpegAsync(outputPath, encoder, cancellationToken);
    }

    private double GetVideoDuration(string filePath)
    {
        try
        {
            return VideoThumbnailExtractor.GetDuration(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetVideoDuration wrapper failed for {File}", Path.GetFileName(filePath));
            return 0;
        }
    }

    private async Task<string?> ExtractBestFrameUsingThumbnailFilterAsync(string filePath, CancellationToken cancellationToken)
    {
        var ffmpegPath = FFmpegInitializer.FfmpegPath;
        if (!File.Exists(ffmpegPath))
            return null;

        var tempDir = Path.Combine(Path.GetTempPath(), "PhotoSort_VideoFrames");
        Directory.CreateDirectory(tempDir);
        var outputPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}.jpg");

        try
        {
            // -ss before -i seeks directly without decoding all prior frames
            // thumbnail=300 limits analysis to 300 frames instead of entire video
            var arguments = $"-ss 1 -i \"{filePath}\" -vf \"thumbnail=300,scale=1024:-2\" -frames:v 1 -q:v 3 \"{outputPath}\"";

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                }
            };

            process.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("FFmpeg timed out for {File}", Path.GetFileName(filePath));
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            var stderr = await stderrTask;

            if (process.ExitCode == 0 && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
                return outputPath;

            _logger.LogWarning("FFmpeg primary extraction failed (exit={ExitCode}) for {File}: {StdErr}",
                process.ExitCode, Path.GetFileName(filePath), stderr[Math.Max(0, stderr.Length - 500)..]);
            TryDelete(outputPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FFmpeg primary extraction error for {File}", Path.GetFileName(filePath));
            TryDelete(outputPath);
            return null;
        }
    }

    private string? ExtractFallbackFrame(string filePath)
    {
        // Try raw FFmpeg first (fast seek)
        var ffmpegPath = FFmpegInitializer.FfmpegPath;
        if (File.Exists(ffmpegPath))
        {
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "PhotoSort_VideoFrames");
                Directory.CreateDirectory(tempDir);
                var outputPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}.jpg");

                var arguments = $"-ss 1 -i \"{filePath}\" -frames:v 1 -q:v 3 \"{outputPath}\"";

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(TimeSpan.FromSeconds(10));

                if (process.ExitCode == 0 && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
                    return outputPath;

                _logger.LogWarning("FFmpeg fallback failed (exit={ExitCode}) for {File}: {StdErr}",
                    process.ExitCode, Path.GetFileName(filePath), stderr[Math.Max(0, stderr.Length - 500)..]);
                TryDelete(outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "FFmpeg fallback error for {File}", Path.GetFileName(filePath));
            }
        }

        // Last resort: use FFMpegCore (different code path, may succeed where raw FFmpeg fails)
        try
        {
            var duration = GetVideoDuration(filePath);
            if (duration > 0)
            {
                var timestamp = Math.Min(1.0, duration * 0.25);
                var result = VideoThumbnailExtractor.ExtractFrameAtTime(filePath, timestamp);
                if (result is not null)
                    return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "FFMpegCore fallback failed for {File}", Path.GetFileName(filePath));
        }

        return null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { /* ignored */ }
    }
}
