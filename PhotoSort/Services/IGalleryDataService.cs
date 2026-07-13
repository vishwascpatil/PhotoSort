using PhotoSort.Models;

namespace PhotoSort.Services;

public interface IGalleryDataService : IDisposable
{
    event EventHandler<IReadOnlyList<GalleryPhoto>>? PhotosLoaded;

    event EventHandler<int>? TotalCountChanged;

    event EventHandler<GalleryMetrics>? MetricsUpdated;

    int TotalCount { get; }

    int LoadedCount { get; }

    GallerySortMode SortMode { get; set; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GalleryPhoto>> LoadInitialPageAsync(
        int pageSize = int.MaxValue,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GalleryPhoto>> LoadNextPageAsync(
        int pageSize = int.MaxValue,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GalleryPhoto>> LoadPageAsync(
        int pageIndex,
        int pageSize = int.MaxValue,
        CancellationToken cancellationToken = default);

    Task RefreshAsync(CancellationToken cancellationToken = default);

    void UpdateViewportSize(int width, int height);

    GalleryMetrics GetCurrentMetrics();
}
