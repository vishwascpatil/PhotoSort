namespace PhotoSort.Models;

public sealed class MemoryResult
{
    public IReadOnlyList<Memory> Memories { get; init; } = [];
    public int TotalCount { get; init; }
    public bool HasMore { get; init; }
}
