namespace PhotoSort.Models;

public sealed class SimilarPhotoItem
{
    public int Id { get; init; }

    public required string FilePath { get; init; }

    public required string FileName { get; init; }

    public long FileSize { get; init; }

    public DateTime? DateTaken { get; init; }

    public string? ThumbnailSmallPath { get; init; }

    public string? FolderPath { get; init; }

    public ulong PerceptualHash { get; init; }

    public int HammingDistance { get; set; }

    public bool IsBest { get; set; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public string DisplayDate => DateTaken?.ToString("MMM dd, yyyy") ?? "Unknown";

    public string DisplaySize => FormatFileSize(FileSize);

    public string DisplayFolder => FolderPath ?? "Unknown";

    public string DisplayDistance => HammingDistance == 0 ? "Exact" : $"Distance: {HammingDistance}";

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
