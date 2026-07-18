namespace PhotoSort.Models;

public sealed class BehaviorSignal
{
    public int ViewCount { get; set; }
    public int ZoomCount { get; set; }
    public int ShareCount { get; set; }
    public int EditCount { get; set; }
    public bool IsFavorite { get; set; }
    public double EngagementScore { get; set; }
    public double Weight { get; set; }
}
