namespace PhotoSort.Models;

public sealed class MemoryPhoto
{
    public Guid MemoryId { get; set; }

    public Memory Memory { get; set; } = null!;

    public int PhotoId { get; set; }
    public int SortOrder { get; set; }
    public string Role { get; set; } = "Supporting";
}
