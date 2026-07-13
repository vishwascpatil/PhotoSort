using FFMpegCore;
using FFMpegCore.Extensions.Downloader;
using FFMpegCore.Extensions.Downloader.Enums;
using Microsoft.Data.Sqlite;

var ffmpegDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "PhotoSort", "ffmpeg");

GlobalFFOptions.Configure(new FFOptions
{
    BinaryFolder = ffmpegDir,
    TemporaryFilesFolder = Path.Combine(ffmpegDir, "tmp")
});

var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "PhotoSort", "PhotoSort.db");

var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();
var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT Id, FilePath FROM Photos WHERE Extension IN ('.mp4','.mov') LIMIT 3";
var rdr = cmd.ExecuteReader();

while (rdr.Read())
{
    var id = rdr.GetInt32(0);
    var filePath = rdr.GetString(1);
    Console.WriteLine($"\n=== Testing Id={id}: {Path.GetFileName(filePath)} ===");

    try
    {
        var analysis = FFProbe.Analyse(filePath);
        Console.WriteLine($"  Duration: {analysis.Duration.TotalSeconds:F2}s");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  FFProbe FAILED: {ex.GetType().Name}: {ex.Message}");
        continue;
    }

    var tempPath = Path.Combine(Path.GetTempPath(), $"photosort_test_{Guid.NewGuid():N}.jpg");
    try
    {
        var success = FFMpeg.Snapshot(
            filePath,
            tempPath,
            size: new System.Drawing.Size(1024, 1024),
            captureTime: TimeSpan.FromSeconds(1.0));

        if (success && File.Exists(tempPath))
        {
            Console.WriteLine($"  Snapshot OK: {new FileInfo(tempPath).Length} bytes");
        }
        else
        {
            Console.WriteLine($"  Snapshot returned false");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Snapshot FAILED: {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        if (File.Exists(tempPath)) File.Delete(tempPath);
    }
}

rdr.Close();
conn.Close();
