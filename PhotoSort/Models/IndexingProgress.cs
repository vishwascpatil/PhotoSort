namespace PhotoSort.Models;

public sealed class IndexingProgress
{
    public int FilesDiscovered;

    public int FilesProcessed;

    public int FilesSkipped;

    public int FilesFailed;

    public string CurrentFolder = string.Empty;

    public string CurrentFile = string.Empty;

    public TimeSpan Elapsed;

    public double PercentComplete => FilesDiscovered > 0
        ? (double)FilesProcessed / FilesDiscovered * 100
        : 0;

    public string StatusMessage => FilesDiscovered == 0
        ? "Discovering files..."
        : $"Processing {FilesProcessed:N0} of {FilesDiscovered:N0} ({PercentComplete:F1}%)";
}
