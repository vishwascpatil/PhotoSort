namespace PhotoSort.Models;

public sealed class PhotoMetadata
{
    public required string FilePath { get; init; }

    public DateTime? DateTaken { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    public string? CameraMake { get; init; }

    public string? CameraModel { get; init; }

    public int? Orientation { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public double? Duration { get; init; }
}
