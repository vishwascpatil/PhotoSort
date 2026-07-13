namespace PhotoSort.Models;

public sealed class VideoThumbnailInformation
{
    public int PhotoId { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public double DurationSeconds { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public string? CodecName { get; init; }
    public double SelectedTimestamp { get; set; }
    public double ThumbnailScore { get; set; }
    public string? SmallPath { get; set; }
    public string? MediumPath { get; set; }
    public string? LargePath { get; set; }
    public DateTime GeneratedDate { get; set; }
    public TimeSpan GenerationTime { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class VideoThumbnailProgress
{
    public int TotalVideos { get; init; }
    public int ProcessedCount { get; init; }
    public int GeneratedCount { get; init; }
    public int FailedCount { get; init; }
    public int SkippedCount { get; init; }
    public int QueueLength { get; init; }
    public long CacheSizeBytes { get; init; }
    public double GenerationRatePerSecond { get; init; }
    public double EstimatedTimeRemainingSeconds { get; init; }
    public TimeSpan AverageGenerationTime { get; init; }
    public string CurrentFileName { get; init; } = string.Empty;
}

public sealed class VideoPreviewStrip
{
    public int PhotoId { get; init; }
    public int FrameCount { get; init; }
    public List<VideoPreviewFrame> Frames { get; init; } = [];
    public DateTime GeneratedDate { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class VideoPreviewFrame
{
    public int Index { get; init; }
    public double Timestamp { get; init; }
    public string ImagePath { get; init; } = string.Empty;
    public double Score { get; set; }
}
