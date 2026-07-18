namespace PhotoSort.Models;

public sealed class TemporalSignal
{
    public int YearDelta { get; set; }
    public double Weight { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public int Hour { get; set; }
    public string? Season { get; set; }
    public string? Holiday { get; set; }
    public bool IsAnniversary { get; set; }
}
