namespace PhotoSort.Models;

public sealed class MemoryCacheEntry
{
    public int Id { get; set; }

    public string Type { get; set; } = "candidate";

    public string? MemoryTypeKey { get; set; }

    public string PhotoIds { get; set; } = "[]";

    public double Score { get; set; }

    public string? Metadata { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
