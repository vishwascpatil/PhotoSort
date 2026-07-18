namespace PhotoSort.Models;

public sealed class MemoryFeedback
{
    public int Id { get; set; }

    public Guid MemoryId { get; set; }

    public Memory Memory { get; set; } = null!;

    public string Feedback { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
