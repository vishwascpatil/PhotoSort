using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Versioning;
using FFMpegCore;

namespace PhotoSort.Services;

[SupportedOSPlatform("windows")]
public static class VideoThumbnailExtractor
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".webm", ".m4v",
        ".mpg", ".mpeg", ".3gp", ".flv", ".ts", ".mts", ".m2ts"
    };

    public static bool IsVideoFile(string filePath)
    {
        return VideoExtensions.Contains(Path.GetExtension(filePath));
    }

    public static double GetDuration(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.WriteLine($"[VideoThumb] File not found: {filePath}");
            return 0;
        }

        try
        {
            var analysis = FFProbe.Analyse(filePath);
            return analysis.Duration.TotalSeconds;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoThumb] FFProbe failed for {Path.GetFileName(filePath)}: {ex.Message}");
            return 0;
        }
    }

    public static Bitmap? ExtractFrameAtTime(string filePath, double timestampSeconds)
    {
        if (!File.Exists(filePath))
            return null;

        var tempPath = Path.Combine(Path.GetTempPath(), $"photosort_frame_{Guid.NewGuid():N}.jpg");

        try
        {
            var success = FFMpeg.Snapshot(
                filePath,
                tempPath,
                size: new Size(1024, 1024),
                captureTime: TimeSpan.FromSeconds(timestampSeconds));

            if (!success || !File.Exists(tempPath))
            {
                Debug.WriteLine($"[VideoThumb] FFMpeg.Snapshot returned false for {Path.GetFileName(filePath)} at {timestampSeconds:F1}s");
                return null;
            }

            using var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read);
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoThumb] ExtractFrame failed for {Path.GetFileName(filePath)} at {timestampSeconds:F1}s: {ex.Message}");
            return null;
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    public static Bitmap? ExtractThumbnail(string filePath, int targetSize)
    {
        if (!File.Exists(filePath))
            return null;

        var tempPath = Path.Combine(Path.GetTempPath(), $"photosort_thumb_{Guid.NewGuid():N}.jpg");

        try
        {
            var success = FFMpeg.Snapshot(
                filePath,
                tempPath,
                size: new Size(targetSize, targetSize));

            if (!success || !File.Exists(tempPath))
                return null;

            using var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read);
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VideoThumb] ExtractThumbnail failed for {Path.GetFileName(filePath)}: {ex.Message}");
            return null;
        }
        finally
        {
            TryDelete(tempPath);
        }
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
