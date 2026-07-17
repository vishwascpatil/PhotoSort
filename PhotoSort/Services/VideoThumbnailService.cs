using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using PhotoSort.Models;

namespace PhotoSort.Services;

[SupportedOSPlatform("windows")]
public sealed class VideoThumbnailService : IVideoThumbnailService
{
    private readonly ILogger<VideoThumbnailService> _logger;
    private readonly string _cacheRoot;
    private readonly string _smallDir;
    private readonly string _mediumDir;
    private readonly string _largeDir;
    private readonly string _previewClipDir;
    private bool _disposed;
    private bool _isInitialized;

    private const int CandidateFrameCount = 8;
    private const double MinBrightness = 0.05;
    private const double MaxBrightness = 0.95;
    private const double MinContrast = 10.0;
    private const int CurrentThumbnailVersion = 1;
    private const int PreviewClipDurationSeconds = 8;

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
        _previewClipDir = Path.Combine(_cacheRoot, "Clips");

        Directory.CreateDirectory(_smallDir);
        Directory.CreateDirectory(_mediumDir);
        Directory.CreateDirectory(_largeDir);
        Directory.CreateDirectory(_previewClipDir);
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
            _logger.LogWarning(ex, "FFmpeg initialization failed, video clips will not be generated");
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

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var duration = GetVideoDuration(filePath);
            if (duration <= 0)
            {
                _logger.LogWarning("Cannot determine duration for {FilePath}", filePath);
                return null;
            }

            var candidates = GenerateCandidateTimestamps(duration);
            var bestTimestamp = 0.0;
            var bestScore = -1.0;
            Bitmap? bestFrame = null;

            foreach (var timestamp in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var frame = ExtractFrameAtTimestamp(filePath, timestamp);
                if (frame is null)
                {
                    _logger.LogDebug("Frame extraction failed at {Timestamp:F1}s for {File}", timestamp, Path.GetFileName(filePath));
                    continue;
                }

                var score = ScoreFrame(frame);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTimestamp = timestamp;
                    bestFrame?.Dispose();
                    bestFrame = frame;
                }
                else
                {
                    frame.Dispose();
                }
            }

            if (bestFrame is null)
            {
                bestFrame = ExtractFrameAtTimestamp(filePath, Math.Min(1.0, duration * 0.1));
                if (bestFrame is null)
                {
                    _logger.LogWarning("Failed to extract any frame from {FilePath}", filePath);
                    return null;
                }
                bestTimestamp = Math.Min(1.0, duration * 0.1);
                bestScore = 0.1;
            }

            var smallPath = GetThumbnailPath(photoId, VideoThumbnailSize.Small);
            var mediumPath = GetThumbnailPath(photoId, VideoThumbnailSize.Medium);
            var largePath = GetThumbnailPath(photoId, VideoThumbnailSize.Large);

            SaveResized(bestFrame, smallPath, 256);
            SaveResized(bestFrame, mediumPath, 512);
            SaveResized(bestFrame, largePath, 1024);

            bestFrame.Dispose();

            sw.Stop();

            var info = new VideoThumbnailInformation
            {
                PhotoId = photoId,
                FilePath = filePath,
                DurationSeconds = duration,
                SelectedTimestamp = bestTimestamp,
                ThumbnailScore = bestScore,
                SmallPath = smallPath,
                MediumPath = mediumPath,
                LargePath = largePath,
                GeneratedDate = DateTime.UtcNow,
                GenerationTime = sw.Elapsed,
                Version = CurrentThumbnailVersion
            };

            _logger.LogDebug(
                "Generated video thumbnails for {PhotoId} at {Timestamp:F2}s (score={Score:F3}) in {Elapsed}ms",
                photoId, bestTimestamp, bestScore, sw.ElapsedMilliseconds);

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

    public string GetPreviewClipPath(int photoId)
    {
        return Path.Combine(_previewClipDir, $"{photoId}.mp4");
    }

    public async Task<string?> GeneratePreviewClipAsync(
        int photoId, string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return null;

        var clipPath = GetPreviewClipPath(photoId);

        if (File.Exists(clipPath) && new FileInfo(clipPath).Length > 0)
            return clipPath;

        var ffmpegPath = FFmpegInitializer.FfmpegPath;
        if (!File.Exists(ffmpegPath))
            return null;

        // Remove any zero-byte orphan from a previous failed attempt
        TryDelete(clipPath);

        try
        {
            var duration = VideoThumbnailExtractor.GetDuration(filePath);
            if (duration <= 0)
                return null;

            var startSeconds = Math.Max(0, duration * 0.15);

            var scaleFilter = $"scale='min(1280,iw)':'min(720,ih)':force_original_aspect_ratio=decrease";
            var arguments = $"-ss {startSeconds:F2} -i \"{filePath}\" " +
                            $"-t {PreviewClipDurationSeconds} " +
                            "-c:v libx264 -preset ultrafast -crf 28 " +
                            $"-vf \"{scaleFilter}\" " +
                            "-b:v 800k -maxrate 1000k -bufsize 2000k " +
                            $"-an -y \"{clipPath}\"";

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            // Read stderr concurrently to prevent ffmpeg hanging when its stderr buffer fills
            process.Start();

            var stderrTask = process.StandardError.ReadToEndAsync();

            // Use a timeout to avoid hanging indefinitely if ffmpeg stalls
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(60));

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout reached — kill the process
                try { process.Kill(entireProcessTree: true); } catch { }
                await stderrTask;
                TryDelete(clipPath);
                _logger.LogWarning("FFmpeg clip generation timed out for {FilePath}", filePath);
                return null;
            }

            var errorOutput = await stderrTask;

            if (process.ExitCode == 0 && File.Exists(clipPath) && new FileInfo(clipPath).Length > 0)
            {
                _logger.LogInformation("Generated {Duration}s preview clip for {PhotoId} at {Start:F1}s ({Size} bytes)",
                    PreviewClipDurationSeconds, photoId, startSeconds,
                    new FileInfo(clipPath).Length);
                return clipPath;
            }

            _logger.LogWarning("FFmpeg clip generation failed for {FilePath} (exit {Code}): {Error}",
                filePath, process.ExitCode, errorOutput[..Math.Min(200, errorOutput.Length)]);

            TryDelete(clipPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to generate preview clip for {FilePath}", filePath);
            TryDelete(clipPath);
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    private static List<double> GenerateCandidateTimestamps(double durationSeconds)
    {
        var timestamps = new List<double>();

        if (durationSeconds <= 5)
        {
            for (int i = 0; i < CandidateFrameCount; i++)
                timestamps.Add(durationSeconds * (i + 1) / (CandidateFrameCount + 1));
        }
        else
        {
            var skipStart = durationSeconds * 0.05;
            var skipEnd = durationSeconds * 0.95;
            var usableDuration = skipEnd - skipStart;

            for (int i = 0; i < CandidateFrameCount; i++)
                timestamps.Add(skipStart + usableDuration * (i + 1) / (CandidateFrameCount + 1));
        }

        return timestamps;
    }

    private static double ScoreFrame(Bitmap frame)
    {
        double brightness = 0;
        double contrast = 0;
        int pixelCount = 0;

        var data = frame.LockBits(
            new Rectangle(0, 0, frame.Width, frame.Height),
            ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        try
        {
            int stride = data.Stride;
            int height = data.Height;
            int width = data.Width;

            double sumBrightness = 0;
            double sumBrightnessSq = 0;

            int sampleStep = Math.Max(1, width / 100);

            var bytes = new byte[stride * height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

            for (int y = 0; y < height; y += sampleStep)
            {
                int rowOffset = y * stride;
                for (int x = 0; x < width; x += sampleStep)
                {
                    int offset = rowOffset + x * 3;
                    double b = bytes[offset] / 255.0;
                    double g = bytes[offset + 1] / 255.0;
                    double r = bytes[offset + 2] / 255.0;

                    double luma = 0.299 * r + 0.587 * g + 0.114 * b;
                    sumBrightness += luma;
                    sumBrightnessSq += luma * luma;
                    pixelCount++;
                }
            }

            if (pixelCount == 0) return 0;

            brightness = sumBrightness / pixelCount;
            double variance = (sumBrightnessSq / pixelCount) - (brightness * brightness);
            contrast = Math.Sqrt(Math.Max(0, variance)) * 255;
        }
        finally
        {
            frame.UnlockBits(data);
        }

        if (brightness < MinBrightness || brightness > MaxBrightness)
            return 0;

        if (contrast < MinContrast)
            return 0;

        double brightnessScore = 1.0 - Math.Abs(brightness - 0.45) / 0.45;
        double contrastScore = Math.Min(1.0, contrast / 80.0);

        return (brightnessScore * 0.5 + contrastScore * 0.5);
    }

    private static void SaveResized(Bitmap source, string outputPath, int targetSize)
    {
        var ratioX = (double)targetSize / source.Width;
        var ratioY = (double)targetSize / source.Height;
        var ratio = Math.Min(ratioX, ratioY);

        var newWidth = Math.Max(1, (int)(source.Width * ratio));
        var newHeight = Math.Max(1, (int)(source.Height * ratio));

        using var bitmap = new Bitmap(newWidth, newHeight);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.DrawImage(source, 0, 0, newWidth, newHeight);

        bitmap.Save(outputPath, ImageFormat.Jpeg);
    }

    private Bitmap? ExtractFrameAtTimestamp(string filePath, double timestampSeconds)
    {
        try
        {
            return VideoThumbnailExtractor.ExtractFrameAtTime(filePath, timestampSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ExtractFrameAtTimestamp wrapper failed for {File} at {Time:F1}s", Path.GetFileName(filePath), timestampSeconds);
            return null;
        }
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
