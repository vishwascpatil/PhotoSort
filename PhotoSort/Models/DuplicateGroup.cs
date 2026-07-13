using System.Collections.ObjectModel;

namespace PhotoSort.Models;

public sealed class DuplicateGroup
{
    public int GroupId { get; init; }

    public required string ContentHash { get; init; }

    public GalleryPhoto? OriginalPhoto { get; set; }

    public ObservableCollection<DuplicatePhoto> Duplicates { get; } = [];

    public long FileSize { get; init; }

    public long PotentialSavings => FileSize * Duplicates.Count;

    public int DuplicateCount => Duplicates.Count;

    public string DisplaySavings => FormatFileSize(PotentialSavings);

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
