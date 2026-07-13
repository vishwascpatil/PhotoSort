using PhotoSort.Models;

namespace PhotoSort.Services;

public interface IPhotoIndexingService
{
    bool IsIndexing { get; }

    Task<IndexingResult> IndexFolderAsync(
        string folderPath,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task CancelIndexingAsync();
}
