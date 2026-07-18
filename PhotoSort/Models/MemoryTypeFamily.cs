namespace PhotoSort.Models;

public sealed class MemoryTypeFamily
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public int SortOrder { get; set; }

    public ICollection<MemoryTypeEntity> MemoryTypes { get; set; } = [];
}
