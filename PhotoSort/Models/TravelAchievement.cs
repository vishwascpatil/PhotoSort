namespace PhotoSort.Models;

public sealed class TravelAchievement
{
    public string Id { get; set; } = string.Empty;

    public string Icon { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsUnlocked { get; set; }

    public DateTime? UnlockedDate { get; set; }

    public double Progress { get; set; }

    public double Target { get; set; }

    public string ProgressDisplay => $"{Progress}/{Target}";

    public double ProgressPercent => Target > 0 ? Math.Min(Progress / Target * 100, 100) : 0;
}
