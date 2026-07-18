namespace PhotoSort.Models;

public sealed class VideoSignal
{
    public double DurationSeconds { get; set; }
    public double BestKeyframeQuality { get; set; }
    public double MotionScore { get; set; }
    public bool HasAudio { get; set; }
    public double Weight { get; set; }
}
