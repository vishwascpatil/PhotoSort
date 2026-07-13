namespace PhotoSort.Models;

public sealed class FaceInfo
{
    public int FaceId { get; init; }

    public int PhotoId { get; init; }

    public string? FilePath { get; init; }

    public string? FileName { get; init; }

    public string? ThumbnailPath { get; init; }

    public int? PersonId { get; init; }

    public string? PersonName { get; init; }

    public double Confidence { get; init; }

    public double BoundingBoxX { get; init; }

    public double BoundingBoxY { get; init; }

    public double BoundingBoxWidth { get; init; }

    public double BoundingBoxHeight { get; init; }

    public DateTime CreatedDate { get; init; }

    public bool IsIgnored { get; init; }

    public bool HasEmbedding { get; init; }

    public string DisplayConfidence => $"{Confidence:P0}";
}
