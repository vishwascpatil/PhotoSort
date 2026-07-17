using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class GalleryDataService : IGalleryDataService
{
    private readonly IPhotoRepository _photoRepository;
    private readonly ILibraryImportOrchestrator _orchestrator;
    private readonly ILogger<GalleryDataService> _logger;

    private const int MaxCachePages = 20;
    private const int DefaultPageSize = int.MaxValue;
    private const int PrefetchPageSize = int.MaxValue;

    private readonly ConcurrentDictionary<int, IReadOnlyList<GalleryPhoto>> _pageCache = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly Stopwatch _latencyStopwatch = new();

    private int _totalCount;
    private int _loadedCount;
    private int _viewportWidth;
    private int _viewportHeight;
    private double _lastQueryLatencyMs;
    private int _currentPageIndex = -1;
    private bool _disposed;
    private bool _initialized;
    private int? _currentFolderId;

    public event EventHandler<IReadOnlyList<GalleryPhoto>>? PhotosLoaded;
    public event EventHandler<int>? TotalCountChanged;
    public event EventHandler<GalleryMetrics>? MetricsUpdated;

    public int TotalCount => Volatile.Read(ref _totalCount);
    public int LoadedCount => Volatile.Read(ref _loadedCount);
    public GallerySortMode SortMode { get; set; } = GallerySortMode.NewestFirst;

    public int? CurrentFolderId
    {
        get => _currentFolderId;
        set
        {
            _currentFolderId = value;
            ClearCache();
            _ = LoadInitialPageAsync();
        }
    }

    public GalleryDataService(
        IPhotoRepository photoRepository,
        ILibraryImportOrchestrator orchestrator,
        ILogger<GalleryDataService> logger)
    {
        _photoRepository = photoRepository;
        _orchestrator = orchestrator;
        _logger = logger;

        _orchestrator.ProgressChanged += OnImportProgressChanged;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        _logger.LogInformation("Initializing gallery data service...");

        var count = await _photoRepository.GetGalleryCountAsync(_currentFolderId);
        Volatile.Write(ref _totalCount, count);
        TotalCountChanged?.Invoke(this, count);

        _initialized = true;

        _logger.LogInformation("Gallery initialized with {Count} photos", count);
    }

    public async Task<IReadOnlyList<GalleryPhoto>> LoadInitialPageAsync(
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            ClearCache();

            _latencyStopwatch.Restart();
            var photos = await _photoRepository.GetGalleryPageAsync(0, pageSize, SortMode, _currentFolderId);
            _latencyStopwatch.Stop();
            _lastQueryLatencyMs = _latencyStopwatch.Elapsed.TotalMilliseconds;

            _pageCache[0] = photos;
            _currentPageIndex = 0;
            Volatile.Write(ref _loadedCount, photos.Count);

            PhotosLoaded?.Invoke(this, photos);
            RaiseMetricsUpdated();

            _ = PrefetchPageAsync(1, cancellationToken);

            _logger.LogDebug(
                "Loaded initial page: {Count} photos in {Latency:F1}ms",
                photos.Count, _lastQueryLatencyMs);

            return photos;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task<IReadOnlyList<GalleryPhoto>> LoadNextPageAsync(
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var nextPageIndex = Volatile.Read(ref _currentPageIndex) + 1;

        if (_pageCache.TryGetValue(nextPageIndex, out var cached))
        {
            _currentPageIndex = nextPageIndex;
            Interlocked.Add(ref _loadedCount, cached.Count);
            PhotosLoaded?.Invoke(this, cached);
            RaiseMetricsUpdated();

            _ = PrefetchPageAsync(nextPageIndex + 1, cancellationToken);

            return cached;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            _latencyStopwatch.Restart();
            var skip = nextPageIndex * pageSize;
            var photos = await _photoRepository.GetGalleryPageAsync(skip, pageSize, SortMode);
            _latencyStopwatch.Stop();
            _lastQueryLatencyMs = _latencyStopwatch.Elapsed.TotalMilliseconds;

            if (photos.Count == 0)
                return [];

            EvictOldPages(nextPageIndex);
            _pageCache[nextPageIndex] = photos;
            _currentPageIndex = nextPageIndex;
            Interlocked.Add(ref _loadedCount, photos.Count);

            PhotosLoaded?.Invoke(this, photos);
            RaiseMetricsUpdated();

            _ = PrefetchPageAsync(nextPageIndex + 1, cancellationToken);

            _logger.LogDebug(
                "Loaded page {PageIndex}: {Count} photos in {Latency:F1}ms",
                nextPageIndex, photos.Count, _lastQueryLatencyMs);

            return photos;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task<IReadOnlyList<GalleryPhoto>> LoadPageAsync(
        int pageIndex,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (_pageCache.TryGetValue(pageIndex, out var cached))
        {
            _currentPageIndex = pageIndex;
            RecalculateLoadedCount();
            PhotosLoaded?.Invoke(this, cached);
            RaiseMetricsUpdated();

            _ = PrefetchPageAsync(pageIndex + 1, cancellationToken);

            return cached;
        }

            await _loadLock.WaitAsync(cancellationToken);
            try
            {
                _latencyStopwatch.Restart();
                var skip = pageIndex * pageSize;
                var photos = await _photoRepository.GetGalleryPageAsync(skip, pageSize, SortMode, _currentFolderId);
                _latencyStopwatch.Stop();
                _lastQueryLatencyMs = _latencyStopwatch.Elapsed.TotalMilliseconds;

                if (photos.Count == 0)
                    return [];

                EvictOldPages(pageIndex);
                _pageCache[pageIndex] = photos;
            _currentPageIndex = pageIndex;
            RecalculateLoadedCount();

            PhotosLoaded?.Invoke(this, photos);
            RaiseMetricsUpdated();

            _ = PrefetchPageAsync(pageIndex + 1, cancellationToken);

            return photos;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing gallery...");

        var newCount = await _photoRepository.GetGalleryCountAsync(_currentFolderId);
        Volatile.Write(ref _totalCount, newCount);
        TotalCountChanged?.Invoke(this, newCount);

        ClearCache();
        Volatile.Write(ref _loadedCount, 0);

        await LoadInitialPageAsync(DefaultPageSize, cancellationToken);

        _logger.LogInformation("Gallery refreshed. Total: {Count}", newCount);
    }

    public void UpdateViewportSize(int width, int height)
    {
        Volatile.Write(ref _viewportWidth, width);
        Volatile.Write(ref _viewportHeight, height);
        RaiseMetricsUpdated();
    }

    public GalleryMetrics GetCurrentMetrics()
    {
        return new GalleryMetrics
        {
            TotalPhotos = TotalCount,
            LoadedPhotos = LoadedCount,
            ViewportWidth = Volatile.Read(ref _viewportWidth),
            ViewportHeight = Volatile.Read(ref _viewportHeight),
            LastQueryLatencyMs = _lastQueryLatencyMs,
            CacheSize = _pageCache.Count
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _orchestrator.ProgressChanged -= OnImportProgressChanged;
        _pageCache.Clear();
        _loadLock.Dispose();
    }

    private async void OnImportProgressChanged(object? sender, LibraryImportProgress e)
    {
        if (e.CurrentStage == "Complete" || e.CurrentStage == "Metadata")
        {
            try
            {
                var newCount = await _photoRepository.GetGalleryCountAsync(_currentFolderId);
                var oldCount = Volatile.Read(ref _totalCount);

                Volatile.Write(ref _totalCount, newCount);
                TotalCountChanged?.Invoke(this, newCount);

                if (newCount > oldCount && _initialized)
                {
                    _logger.LogInformation(
                        "New photos detected: {Old} -> {New}. Refreshing gallery.",
                        oldCount, newCount);

                    await RefreshAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh gallery after import update");
            }
        }
    }

    private async Task PrefetchPageAsync(int pageIndex, CancellationToken cancellationToken)
    {
        if (_pageCache.ContainsKey(pageIndex))
            return;

        try
        {
            await Task.Delay(50, cancellationToken);

            _latencyStopwatch.Restart();
            var skip = pageIndex * DefaultPageSize;
            var photos = await _photoRepository.GetGalleryPageAsync(
                skip, PrefetchPageSize, SortMode, _currentFolderId);
            _latencyStopwatch.Stop();

            if (photos.Count > 0 && !_disposed && !cancellationToken.IsCancellationRequested)
            {
                EvictOldPages(pageIndex);
                _pageCache[pageIndex] = photos;

                _logger.LogDebug(
                    "Prefetched page {PageIndex}: {Count} photos in {Latency:F1}ms",
                    pageIndex, photos.Count, _latencyStopwatch.Elapsed.TotalMilliseconds);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Prefetch failed for page {PageIndex}", pageIndex);
        }
    }

    private void EvictOldPages(int currentPage)
    {
        if (_pageCache.Count < MaxCachePages)
            return;

        var pagesToEvict = _pageCache.Keys
            .Where(k => Math.Abs(k - currentPage) > MaxCachePages / 2)
            .ToList();

        foreach (var page in pagesToEvict)
        {
            _pageCache.TryRemove(page, out _);
        }
    }

    private void ClearCache()
    {
        _pageCache.Clear();
        _currentPageIndex = -1;
    }

    private void RecalculateLoadedCount()
    {
        var total = 0;
        foreach (var page in _pageCache.Values)
        {
            total += page.Count;
        }
        Volatile.Write(ref _loadedCount, total);
    }

    private void RaiseMetricsUpdated()
    {
        MetricsUpdated?.Invoke(this, GetCurrentMetrics());
    }
}
