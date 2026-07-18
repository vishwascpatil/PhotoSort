namespace PhotoSort.Models;

public sealed class MemoryScore
{
    public Guid MemoryId { get; set; }

    public Memory Memory { get; set; } = null!;

    public int PhotoId { get; set; }

    public double? Sharpness { get; set; }

    public double? Brightness { get; set; }

    public double? Noise { get; set; }

    public double? Composition { get; set; }

    public int? FaceCount { get; set; }

    public double? SmileScore { get; set; }

    public double? EyeOpenness { get; set; }

    public double? QualityScore { get; set; }

    public double? Importance { get; set; }

    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}
