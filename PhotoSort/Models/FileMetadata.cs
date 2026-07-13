namespace PhotoSort.Models;

public sealed class FileMetadata
{
    public required string FilePath { get; init; }

    public required string FileName { get; init; }

    public required string Extension { get; init; }

    public long FileSize { get; init; }

    public DateTime CreatedDateUtc { get; init; }

    public DateTime ModifiedDateUtc { get; init; }
}
