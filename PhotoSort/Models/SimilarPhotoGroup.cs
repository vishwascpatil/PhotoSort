using System.Collections.ObjectModel;

namespace PhotoSort.Models;

public enum SimilarityLevel
{
    ExactMatch,
    VerySimilar,
    Similar
}

public sealed class SimilarPhotoGroup
{
    public int GroupId { get; init; }

    public ulong ReferenceHash { get; init; }

    public SimilarityLevel Level { get; init; }

    public GalleryPhoto? BestPhoto { get; set; }

    public ObservableCollection<SimilarPhotoItem> SimilarPhotos { get; } = [];

    public long FileSize { get; init; }

    public long PotentialSavings => SimilarPhotos.Where(p => !p.IsBest).Sum(p => p.FileSize);

    public int SimilarCount => SimilarPhotos.Count(p => !p.IsBest);

    public int GroupSize => SimilarPhotos.Count;

    public string DisplaySavings => FormatFileSize(PotentialSavings);

    public string DisplayLevel => Level switch
    {
        SimilarityLevel.ExactMatch => "Exact Match",
        SimilarityLevel.VerySimilar => "Very Similar",
        SimilarityLevel.Similar => "Similar",
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
