namespace PhotoSort.Models;

public sealed class MemoryHighlight
{
    public string Id { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? PhotoPath { get; set; }

    public int? PhotoId { get; set; }

    public DateTime? Date { get; set; }

    public string? Location { get; set; }
}
