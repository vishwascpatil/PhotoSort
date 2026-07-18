namespace PhotoSort.Models;

public sealed class GalleryPhoto
{
    public static readonly HashSet<string> ImageExtensions =
    [
        ".jpg", ".jpeg", ".png", ".heic", ".webp", ".bmp", ".gif", ".tiff", ".tif"
    ];

    public static readonly HashSet<string> VideoExtensions =
    [
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp", ".flv", ".ts", ".mts", ".m2ts"
    ];


    public int Id { get; init; }

    public required string FilePath { get; init; }

    public required string FileName { get; init; }

    public required string Extension { get; init; }

    public DateTime? DateTaken { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public long FileSize { get; init; }

    public string? ThumbnailPath { get; init; }

    public string? ThumbnailSmallPath { get; init; }

    public string? ThumbnailMediumPath { get; init; }

    public string? VideoThumbnailSmallPath { get; init; }

    public string? VideoThumbnailMediumPath { get; init; }

    public string? VideoThumbnailLargePath { get; init; }

    public bool IsFavorite { get; init; }

    public DateTime ModifiedDateUtc { get; init; }

    public int FolderId { get; init; }

    public ProcessingState State { get; init; }

    public bool IsVideo => string.Equals(Extension, ".mp4", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Extension, ".mov", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Extension, ".avi", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Extension, ".mkv", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Extension, ".wmv", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Extension, ".webm", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Extension, ".m4v", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Extension, ".mpg", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Extension, ".mpeg", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Extension, ".3gp", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Extension, ".flv", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Extension, ".ts", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Extension, ".mts", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Extension, ".m2ts", StringComparison.OrdinalIgnoreCase);

    public string? EffectiveSmallThumbnail => VideoThumbnailSmallPath ?? ThumbnailSmallPath;
    public string? EffectiveMediumThumbnail => VideoThumbnailMediumPath ?? ThumbnailMediumPath;

    public int? DateTakenYear { get; init; }

    public int? DateTakenMonth { get; init; }

    public int? DateTakenDay { get; init; }

    public string DisplayDate => DateTaken?.ToString("MMM dd, yyyy") ?? ModifiedDateUtc.ToString("MMM dd, yyyy");

    public string DisplaySize => FormatFileSize(FileSize);

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
