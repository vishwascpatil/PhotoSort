namespace PhotoSort.Models;

public sealed class TimelineStats
{
    public int TotalPhotos { get; init; }

    public int YearsCount { get; init; }

    public int MonthsCount { get; init; }

    public int DaysCount { get; init; }

    public int? EarliestYear { get; init; }

    public int? LatestYear { get; init; }

    public double QueryLatencyMs { get; init; }
}
