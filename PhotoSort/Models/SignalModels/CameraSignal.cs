namespace PhotoSort.Models;

public sealed class CameraSignal
{
    public string? Make { get; set; }
    public string? Model { get; set; }
    public double FocalLength35mm { get; set; }
    public double Aperture { get; set; }
    public int Iso { get; set; }
    public string? ExposureTime { get; set; }
    public bool IsDSLR { get; set; }
    public bool IsPortraitMode { get; set; }
    public bool IsMacro { get; set; }
    public bool IsLongExposure { get; set; }
    public double Weight { get; set; }
}
