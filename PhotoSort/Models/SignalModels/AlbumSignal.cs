namespace PhotoSort.Models;

public sealed class AlbumSignal
{
    public List<int> AlbumIds { get; set; } = [];
    public int AlbumCount { get; set; }
    public double Weight { get; set; }
}
