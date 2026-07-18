namespace PhotoSort.Models;

public sealed class BurstSignal
{
    public string? BurstGroupId { get; set; }
    public int GroupCount { get; set; }
    public int BestPhotoId { get; set; }
    public List<int> AlternatePhotoIds { get; set; } = [];
    public double Weight { get; set; }
}
