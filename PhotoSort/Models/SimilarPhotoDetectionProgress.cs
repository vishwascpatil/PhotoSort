namespace PhotoSort.Models;

public enum SimilarDetectionPhase
{
    Idle,
    ThumbnailScan,
    HashGeneration,
    Comparison,
    Grouping,
    Completed,
    Paused
}

public sealed class SimilarPhotoDetectionProgress
{
    public SimilarDetectionPhase Phase { get; set; }

    public int TotalPhotos { get; set; }

    public int ThumbnailsAvailable { get; set; }

    public int HashesGenerated { get; set; }

    public int PhotosCompared { get; set; }

    public int GroupsFound { get; set; }

    public long ReclaimableBytes { get; set; }

    public double ProgressPercent => TotalPhotos > 0
        ? (double)HashesGenerated / TotalPhotos * 100
        : 0;

    public string DisplayReclaimable => FormatFileSize(ReclaimableBytes);

    public string PhaseDisplay => Phase switch
    {
        SimilarDetectionPhase.Idle => "Ready",
        SimilarDetectionPhase.ThumbnailScan => "Scanning thumbnails...",
        SimilarDetectionPhase.HashGeneration => "Generating perceptual hashes...",
        SimilarDetectionPhase.Comparison => "Comparing hashes...",
        SimilarDetectionPhase.Grouping => "Grouping similar photos...",
        SimilarDetectionPhase.Completed => "Completed",
        SimilarDetectionPhase.Paused => "Paused",
        _ => "Unknown"
    };

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }
}
