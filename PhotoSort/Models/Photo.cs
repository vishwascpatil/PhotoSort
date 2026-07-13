namespace PhotoSort.Models;

public sealed class Photo
{
    public int Id { get; set; }

    public required string FilePath { get; set; }

    public required string FileName { get; set; }

    public required string Extension { get; set; }

    public DateTime? DateTaken { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public long FileSize { get; set; }

    public string? ThumbnailPath { get; set; }

    public string? ThumbnailSmallPath { get; set; }

    public string? ThumbnailMediumPath { get; set; }

    public DateTime? ThumbnailGeneratedDate { get; set; }

    public bool IsFavorite { get; set; }

    public DateTime ModifiedDateUtc { get; set; }

    public int FolderId { get; set; }

    public Folder Folder { get; set; } = null!;

    public string? CameraMake { get; set; }

    public string? CameraModel { get; set; }

    public int? Orientation { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public double? Duration { get; set; }

    public ProcessingState State { get; set; } = ProcessingState.NotIndexed;

    public DateTime? MetadataExtractedDate { get; set; }

    public string? ContentHash { get; set; }

    public DateTime? HashCalculatedDate { get; set; }

    public int? DuplicateGroupId { get; set; }

    public MediaCategory MediaCategory { get; set; } = MediaCategory.Unknown;

    public double ClassificationConfidence { get; set; }

    public DateTime? ClassificationDate { get; set; }

    public ulong? PerceptualHash { get; set; }

    public DateTime? PerceptualHashDate { get; set; }

    public int? SimilarPhotoGroupId { get; set; }

    public ICollection<Face> Faces { get; set; } = [];

    public ICollection<PhotoPlace> PhotoPlaces { get; set; } = [];

    public ICollection<PhotoTag> PhotoTags { get; set; } = [];

    public string? VideoThumbnailSmallPath { get; set; }
    public string? VideoThumbnailMediumPath { get; set; }
    public string? VideoThumbnailLargePath { get; set; }
    public double? VideoThumbnailTimestamp { get; set; }
    public double? VideoThumbnailScore { get; set; }
    public int VideoThumbnailVersion { get; set; }
    public DateTime? VideoThumbnailDate { get; set; }
    public string? PreviewClipPath { get; set; }
    public bool PreviewStripGenerated { get; set; }
    public int PreviewStripVersion { get; set; }
    public int PreviewFrameCount { get; set; }
}
