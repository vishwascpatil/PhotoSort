namespace PhotoSort.Models;

public sealed class TravelSummaryCard
{
    public string Icon { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Subtitle { get; set; }

    public string? Trend { get; set; }
}
