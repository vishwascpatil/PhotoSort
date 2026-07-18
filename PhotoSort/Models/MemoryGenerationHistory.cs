namespace PhotoSort.Models;

public sealed class MemoryGenerationHistory
{
    public int Id { get; set; }

    public Guid RunId { get; set; }

    public string Stage { get; set; } = string.Empty;

    public string? MemoryTypeKey { get; set; }

    public int CandidatesIn { get; set; }

    public int CandidatesOut { get; set; }

    public long DurationMs { get; set; }

    public string? Error { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime CompletedAt { get; set; }
}
