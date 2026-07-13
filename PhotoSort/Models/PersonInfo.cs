namespace PhotoSort.Models;

public sealed class PersonInfo
{
    public int PersonId { get; init; }

    public required string Name { get; init; }

    public int FaceCount { get; init; }

    public int PhotoCount { get; init; }

    public string? ThumbnailPath { get; init; }

    public DateTime? LastSeenDate { get; init; }

    public DateTime CreatedDate { get; init; }

    public string DisplayLastSeen => LastSeenDate?.ToString("MMM dd, yyyy") ?? "Unknown";

    public string DisplayFaceCount => $"{FaceCount} face{(FaceCount != 1 ? "s" : "")}";

    public string DisplayPhotoCount => $"{PhotoCount} photo{(PhotoCount != 1 ? "s" : "")}";
}
