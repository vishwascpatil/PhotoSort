namespace PhotoSort.Models;

public sealed class CleanupCategory
{
    public MediaCategory Category { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string Icon { get; init; } = string.Empty;

    public int FileCount { get; set; }

    public long TotalSize { get; set; }

    public long PotentialSavings { get; set; }

    public bool IsSelected { get; set; }

    public string DisplayTotalSize => FormatFileSize(TotalSize);

    public string DisplayPotentialSavings => FormatFileSize(PotentialSavings);

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
