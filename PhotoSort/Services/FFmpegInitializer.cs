using System.Diagnostics;
using System.IO;
using FFMpegCore;
using FFMpegCore.Extensions.Downloader;
using FFMpegCore.Extensions.Downloader.Enums;

namespace PhotoSort.Services;

public static class FFmpegInitializer
{
    private static bool _initialized;
    private static readonly object _lock = new();

    public static string FfmpegDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhotoSort", "ffmpeg");

    public static string FfmpegPath => Path.Combine(FfmpegDirectory, "ffmpeg.exe");
    public static string FfprobePath => Path.Combine(FfmpegDirectory, "ffprobe.exe");

    public static async Task<bool> EnsureFFmpegAsync()
    {
        lock (_lock)
        {
            if (_initialized) return File.Exists(FfmpegPath);
        }

        Directory.CreateDirectory(FfmpegDirectory);

        GlobalFFOptions.Configure(new FFOptions
        {
            BinaryFolder = FfmpegDirectory,
            TemporaryFilesFolder = Path.Combine(FfmpegDirectory, "tmp"),
            FFMpegPath = FfmpegPath,
            FFProbePath = FfprobePath
        });

        if (File.Exists(FfmpegPath) && File.Exists(FfprobePath))
        {
            lock (_lock) { _initialized = true; }
            Debug.WriteLine("[FFmpeg] Binaries already present");
            return true;
        }

        try
        {
            Debug.WriteLine("[FFmpeg] Downloading binaries...");
            var downloaded = await FFMpegDownloader.DownloadBinaries(
                FFMpegVersions.LatestAvailable,
                FFMpegBinaries.FFMpeg | FFMpegBinaries.FFProbe);

            Debug.WriteLine($"[FFmpeg] Downloaded {downloaded.Count} file(s): {string.Join(", ", downloaded)}");

            lock (_lock) { _initialized = true; }
            return File.Exists(FfmpegPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FFmpeg] Download failed: {ex.Message}");
            return false;
        }
    }
}
