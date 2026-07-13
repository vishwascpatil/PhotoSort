namespace PhotoSort.Models;

public enum FileSystemChange
{
    Created,
    Modified,
    Deleted,
    Renamed
}

public sealed class FileSystemEvent
{
    public required string FilePath { get; init; }

    public required FileSystemChange ChangeType { get; init; }

    public string? OldFilePath { get; init; }

    public required string WatchedFolderPath { get; init; }

    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public bool IsDirectory { get; init; }
}
