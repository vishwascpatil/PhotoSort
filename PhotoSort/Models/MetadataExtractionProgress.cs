namespace PhotoSort.Models;

public sealed class MetadataExtractionProgress
{
    public int FilesQueued;

    public int FilesProcessed;

    public int FilesSkipped;

    public int FilesFailed;

    public string CurrentFile = string.Empty;

    public TimeSpan Elapsed;

    public double PercentComplete => FilesQueued > 0
        ? (double)FilesProcessed / FilesQueued * 100
        : 0;

    public string StatusMessage => FilesQueued == 0
        ? "Preparing..."
        : $"Extracted {FilesProcessed:N0} of {FilesQueued:N0} ({PercentComplete:F1}%)";
}
