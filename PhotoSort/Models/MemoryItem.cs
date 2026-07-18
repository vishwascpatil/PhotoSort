namespace PhotoSort.Models;

public sealed class MemoryItem
{
    public Guid MemoryId { get; set; }

    public Memory Memory { get; set; } = null!;

    public int PhotoId { get; set; }

    public int SortOrder { get; set; }

    public string Role { get; set; } = "supporting";

    public double? QualityScore { get; set; }
}
