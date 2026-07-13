namespace PhotoSort.Models;

public sealed class FaceDetectionResult
{
    public int PhotoId { get; init; }

    public required string FilePath { get; init; }

    public int FacesDetected { get; init; }

    public IReadOnlyList<DetectedFace> Faces { get; init; } = [];

    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public string? ModelVersion { get; init; }
}

public sealed class DetectedFace
{
    public double BoundingBoxX { get; init; }

    public double BoundingBoxY { get; init; }

    public double BoundingBoxWidth { get; init; }

    public double BoundingBoxHeight { get; init; }

    public double Confidence { get; init; }

    public double[]? Landmarks { get; init; }

    public double FaceAngle { get; init; }

    public double FaceSize { get; init; }

    public string? ModelVersion { get; init; }

    public byte[]? AlignedFaceData { get; init; }
}
