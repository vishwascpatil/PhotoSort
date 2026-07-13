using System.IO;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class FileWatcherEventArgs : EventArgs
{
    public required string FilePath { get; init; }

    public required FileWatcherChange ChangeType { get; init; }

    public string? OldFilePath { get; init; }

    public required string WatchedFolderPath { get; init; }
}

public enum FileWatcherChange
{
    Created,
    Modified,
    Deleted,
    Renamed
}

public interface IFileWatcherService : IDisposable
{
    bool IsWatching { get; }

    int WatchedFolderCount { get; }

    IReadOnlyList<string> WatchedFolders { get; }

    event EventHandler<FileWatcherEventArgs>? FileChanged;

    event EventHandler<FileSystemWatcher>? OverflowOccurred;

    void StartWatching(string folderPath);

    void StartWatching(IEnumerable<string> folderPaths);

    void StopWatching();

    void StopWatching(string folderPath);

    void StopWatchingAll();

    bool IsWatchingFolder(string folderPath);
}
