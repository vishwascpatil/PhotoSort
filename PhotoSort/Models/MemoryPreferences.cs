namespace PhotoSort.Models;

public sealed class MemoryPreferences
{
    public int Id { get; set; }

    public int MaxDailyMemories { get; set; } = 10;

    public string? PreferredTypes { get; set; }

    public string? ExcludedTypes { get; set; }

    public double MinScore { get; set; } = 0.3;

    public bool IncludeScreenshots { get; set; }

    public string WeekdayMode { get; set; } = "balanced";

    public string MusicPreference { get; set; } = "none";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
