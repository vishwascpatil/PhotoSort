namespace PhotoSort.Models;

public sealed class Face
{
    public int Id { get; set; }

    public int PhotoId { get; set; }

    public Photo Photo { get; set; } = null!;

    public double BoundingBoxX { get; set; }

    public double BoundingBoxY { get; set; }

    public double BoundingBoxWidth { get; set; }

    public double BoundingBoxHeight { get; set; }

    public double Confidence { get; set; }

    public double LandmarkX1 { get; set; }

    public double LandmarkY1 { get; set; }

    public double LandmarkX2 { get; set; }

    public double LandmarkY2 { get; set; }

    public double LandmarkX3 { get; set; }

    public double LandmarkY3 { get; set; }

    public double LandmarkX4 { get; set; }

    public double LandmarkY4 { get; set; }

    public double LandmarkX5 { get; set; }

    public double LandmarkY5 { get; set; }

    public double FaceAngle { get; set; }

    public double FaceSize { get; set; }

    public string? DetectionModelVersion { get; set; }

    public bool IsIgnored { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public string? ThumbnailPath { get; set; }

    public RecognitionState RecognitionState { get; set; } = RecognitionState.NotProcessed;

    public double RecognitionConfidence { get; set; }

    public DateTime? LastRecognitionDate { get; set; }

    public ICollection<PersonFace> PersonFaces { get; set; } = [];

    public FaceEmbedding? FaceEmbedding { get; set; }
}

public enum RecognitionState
{
    NotProcessed = 0,
    Detected = 1,
    Embedded = 2,
    Clustered = 3,
    Assigned = 4,
    Ignored = 5,
    Failed = 99
}
