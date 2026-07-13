namespace PhotoSort.Models;

public sealed class CleanupStatistics
{
    public int TotalFiles { get; init; }

    public int ClassifiedFiles { get; init; }

    public int UnclassifiedFiles { get; init; }

    public long TotalSize { get; init; }

    public long ClassifiableSize { get; init; }

    public double ClassificationRate => TotalFiles > 0
        ? (double)ClassifiedFiles / TotalFiles * 100
        : 0;

    public string DisplayTotalSize => FormatFileSize(TotalSize);

    public string DisplayClassifiableSize => FormatFileSize(ClassifiableSize);

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
