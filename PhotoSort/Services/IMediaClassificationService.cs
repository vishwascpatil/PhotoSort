using PhotoSort.Models;

namespace PhotoSort.Services;

public interface IMediaClassificationService : IDisposable
{
    event EventHandler<int>? ProgressChanged;

    Task<ClassificationResult> ClassifyAsync(Photo photo, string folderPath);
    Task<int> ClassifyAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CleanupCategory>> GetCleanupCategoriesAsync();
    Task<IReadOnlyList<GalleryPhoto>> GetPhotosByCategoryAsync(MediaCategory category, int skip = 0, int take = 100);
    Task<CleanupStatistics> GetStatisticsAsync();
    Task DeletePhotosByCategoryAsync(MediaCategory category, IReadOnlyList<int> photoIds);
    MediaCategory ClassifyByPath(string filePath, string folderPath);
    MediaCategory ClassifyByFilename(string fileName);
    MediaCategory ClassifyByMetadata(Photo photo);
}
