using PhotoSort.Models;

namespace PhotoSort.Services;

public interface ILibraryImportOrchestrator
{
    bool IsRunning { get; }

    bool IsPaused { get; }

    LibraryImportProgress CurrentProgress { get; }

    event EventHandler<LibraryImportProgress>? ProgressChanged;

    event EventHandler<string>? StageChanged;

    Task<ImportResult> ImportFolderAsync(
        string folderPath,
        IProgress<LibraryImportProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task PauseAsync();

    Task ResumeAsync();

    Task CancelAsync();

    Task RecoverIncompleteImportsAsync();
}
