namespace PhotoSort.Models;

public sealed class MemoryStatistics
{
    public int MemoryTypeId { get; set; }

    public MemoryTypeEntity MemoryType { get; set; } = null!;

    public int TotalGenerated { get; set; }

    public int TotalViewed { get; set; }

    public double AvgScore { get; set; }

    public DateTime? LastGeneratedAt { get; set; }

    public int UserViewCount { get; set; }

    public long UserDwellSeconds { get; set; }

    public int UserFavoriteCount { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
