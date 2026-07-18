using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotoSort.Models;
using PhotoSort.Services;

namespace PhotoSort.ViewModels;

public partial class GalleryViewModel : ObservableObject, IDisposable
{
    private readonly IGalleryDataService _galleryDataService;
    private readonly INavigationService _navigationService;
    private readonly IThumbnailCacheService _thumbnailCacheService;
    private readonly IVideoThumbnailWorker _videoThumbnailWorker;
    private readonly ITimelineService _timelineService;
    private readonly ILogger<GalleryViewModel> _logger;

    private bool _isLoading;
    private bool _disposed;
    private bool _thumbnailsStarted;
    private readonly Stopwatch _loadingStopwatch = new();
    private readonly DispatcherTimer _loadingTimer;

    [ObservableProperty]
    private string _loadingElapsed = string.Empty;

    [ObservableProperty]
    private bool _hasMorePages = true;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _loadedCount;

    [ObservableProperty]
    private string _metricsSummary = string.Empty;

    [ObservableProperty]
    private GallerySortMode _currentSortMode = GallerySortMode.NewestFirst;

    [ObservableProperty]
    private bool _isGalleryEmpty = true;

    [ObservableProperty]
    private string _memoryEstimate = "0 MB";

    [ObservableProperty]
    private double _lastQueryLatencyMs;

    [ObservableProperty]
    private string _thumbnailStatus = string.Empty;

    [ObservableProperty]
    private string _photoThumbnailStatus = string.Empty;

    [ObservableProperty]
    private string _videoThumbnailStatus = string.Empty;

    [ObservableProperty]
    private bool _isTimelineView;

    [ObservableProperty]
    private string _viewModeText = "Timeline";

    [ObservableProperty]
    private int _timelineYearCount;

    [ObservableProperty]
    private int _timelineMonthCount;

    [ObservableProperty]
    private int _timelineDayCount;

    public string TimelineSummary => $"{TimelineYearCount} years  |  {TimelineMonthCount} months  |  {TimelineDayCount} days";

    public ObservableCollection<GalleryPhoto> Photos { get; } = [];
    public ObservableCollection<TimelineYearGroup> TimelineGroups { get; } = [];

    public GalleryViewModel(
        IGalleryDataService galleryDataService,
        INavigationService navigationService,
        IThumbnailCacheService thumbnailCacheService,
        IVideoThumbnailWorker videoThumbnailWorker,
        ITimelineService timelineService,
        ILogger<GalleryViewModel> logger)
    {
        _galleryDataService = galleryDataService;
        _navigationService = navigationService;
        _thumbnailCacheService = thumbnailCacheService;
        _videoThumbnailWorker = videoThumbnailWorker;
        _timelineService = timelineService;
        _logger = logger;

        _loadingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _loadingTimer.Tick += OnLoadingTimerTick;

        _galleryDataService.PhotosLoaded += OnPhotosLoaded;
        _galleryDataService.TotalCountChanged += OnTotalCountChanged;
        _galleryDataService.MetricsUpdated += OnMetricsUpdated;

        _thumbnailCacheService.ThumbnailReady += OnThumbnailReady;
        _thumbnailCacheService.ProgressChanged += OnThumbnailProgressChanged;
        _videoThumbnailWorker.ThumbnailReady += OnVideoThumbnailReady;
    }

    [RelayCommand]
    private async Task LoadInitialAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        HasMorePages = true;
        LoadingElapsed = string.Empty;
        _loadingStopwatch.Restart();
        _loadingTimer.Start();

        try
        {
            if (!_thumbnailsStarted)
            {
                _thumbnailsStarted = true;

                var videoThumbnailService = App.Services.GetRequiredService<IVideoThumbnailService>();
                await videoThumbnailService.InitializeAsync();

                try { await _thumbnailCacheService.StartAsync(); }
                catch (Exception ex) { _logger.LogError(ex, "ThumbnailCacheService start failed"); }

                try { await _videoThumbnailWorker.StartAsync(); }
                catch (Exception ex) { _logger.LogError(ex, "VideoThumbnailWorker start failed"); }
            }

            if (IsTimelineView)
            {
                await LoadTimelineAsync();
            }
            else
            {
                await _galleryDataService.InitializeAsync();
                var photos = await _galleryDataService.LoadInitialPageAsync();

Photos.Clear();
            foreach (var photo in photos)
            {
                Photos.Add(photo);
            }

            IsGalleryEmpty = Photos.Count == 0;
            HasMorePages = photos.Count > 0;

            var captured = photos;
            _ = Task.Run(() =>
            {
                try { EnqueueThumbnails(captured, ThumbnailPriority.High); }
                catch (Exception ex) { _logger.LogError(ex, "Thumbnail enqueue failed"); }
            });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load initial gallery page");
        }
        finally
        {
            _loadingTimer.Stop();
            _loadingStopwatch.Stop();
            LoadingElapsed = $"Loaded in {_loadingStopwatch.Elapsed.TotalSeconds:F1}s";
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (_isLoading || !HasMorePages || IsTimelineView)
            return;

        _isLoading = true;

        try
        {
            var photos = await _galleryDataService.LoadNextPageAsync();

            if (photos.Count == 0)
            {
                HasMorePages = false;
                return;
            }

            foreach (var photo in photos)
            {
                Photos.Add(photo);
            }

            HasMorePages = photos.Count > 0;

            var captured = photos;
            _ = Task.Run(() =>
            {
                try { EnqueueThumbnails(captured, ThumbnailPriority.Medium); }
                catch (Exception ex) { _logger.LogError(ex, "Thumbnail enqueue failed"); }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load more gallery photos");
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task ToggleViewModeAsync()
    {
        IsTimelineView = !IsTimelineView;
        ViewModeText = IsTimelineView ? "All Photos" : "Timeline";

        Photos.Clear();
        TimelineGroups.Clear();
        HasMorePages = true;

        await LoadInitialAsync();
    }

    [RelayCommand]
    private async Task ToggleSortModeAsync()
    {
        CurrentSortMode = CurrentSortMode == GallerySortMode.NewestFirst
            ? GallerySortMode.OldestFirst
            : GallerySortMode.NewestFirst;

        _galleryDataService.SortMode = CurrentSortMode;

        Photos.Clear();
        HasMorePages = true;
        await LoadInitialAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Photos.Clear();
        TimelineGroups.Clear();
        HasMorePages = true;
        await LoadInitialAsync();
    }

    [RelayCommand]
    private void OpenPhoto(object? parameter)
    {
        if (parameter is not int photoId)
            return;

        var allPhotos = GetAllVisiblePhotos();
        var index = allPhotos.FindIndex(p => p.Id == photoId);
        if (index < 0)
            return;

        var photo = allPhotos[index];

        if (photo.IsVideo)
        {
            var videoViewer = App.Services.GetRequiredService<VideoViewerViewModel>();
            videoViewer.Initialize(photo);
            _navigationService.NavigateTo(videoViewer);
        }
        else
        {
            var viewer = App.Services.GetRequiredService<PhotoViewerViewModel>();
            viewer.Initialize(allPhotos, index);
            _navigationService.NavigateTo(viewer);
        }
    }

    [RelayCommand]
    private void FindDuplicates()
    {
        _navigationService.NavigateTo<DuplicateDetectionViewModel>();
    }

    [RelayCommand]
    private void FindCleanup()
    {
        _navigationService.NavigateTo<CleanupViewModel>();
    }

    [RelayCommand]
    private void FindSimilarPhotos()
    {
        _navigationService.NavigateTo<SimilarPhotosViewModel>();
    }

    [RelayCommand]
    private void FindSync()
    {
        _navigationService.NavigateTo<SyncViewModel>();
    }

    [RelayCommand]
    private void FindPeople()
    {
        _navigationService.NavigateTo<PeopleViewModel>();
    }

    [RelayCommand]
    private void FindTravelInsights()
    {
        _navigationService.NavigateTo<TravelInsightsViewModel>();
    }

    [RelayCommand]
    private async Task ToggleYearAsync(TimelineYearGroup? yearGroup)
    {
        if (yearGroup is null)
            return;

        yearGroup.IsExpanded = !yearGroup.IsExpanded;

        if (yearGroup.IsExpanded && yearGroup.Months.Count == 0)
        {
            var months = await _timelineService.GetMonthGroupsAsync(yearGroup.Year);
            foreach (var month in months)
            {
                yearGroup.Months.Add(month);
            }
        }
    }

    [RelayCommand]
    private async Task ToggleMonthAsync(TimelineMonthGroup? monthGroup)
    {
        if (monthGroup is null)
            return;

        monthGroup.IsExpanded = !monthGroup.IsExpanded;

        if (monthGroup.IsExpanded && monthGroup.Days.Count == 0)
        {
            var days = await _timelineService.GetDayGroupsAsync(monthGroup.Year, monthGroup.Month);
            foreach (var day in days)
            {
                monthGroup.Days.Add(day);
            }
        }
    }

    [RelayCommand]
    private async Task ToggleDayAsync(TimelineDayGroup? dayGroup)
    {
        if (dayGroup is null)
            return;

        if (!dayGroup.IsLoaded)
        {
            var photos = await _timelineService.GetPhotosForDayAsync(
                dayGroup.Year, dayGroup.Month, dayGroup.Day);

            dayGroup.Photos.Clear();
            foreach (var photo in photos)
            {
                dayGroup.Photos.Add(photo);
            }

            dayGroup.IsLoaded = true;

            EnqueueThumbnails(photos, ThumbnailPriority.High);
        }
    }

    public void OnViewportChanged(int width, int height)
    {
        _galleryDataService.UpdateViewportSize(width, height);
    }

    public bool GetIsLoading()
    {
        return _isLoading;
    }

    private async Task LoadTimelineAsync()
    {
        var sw = Stopwatch.StartNew();

        var yearGroups = await _timelineService.GetYearGroupsAsync();

        TimelineGroups.Clear();
        foreach (var year in yearGroups)
        {
            TimelineGroups.Add(year);
        }

        var stats = await _timelineService.GetTimelineStatsAsync();
        TimelineYearCount = stats.YearsCount;
        TimelineMonthCount = stats.MonthsCount;
        TimelineDayCount = stats.DaysCount;

        sw.Stop();
        LastQueryLatencyMs = sw.Elapsed.TotalMilliseconds;

        IsGalleryEmpty = TimelineGroups.Count == 0;
        TotalCount = stats.TotalPhotos;
        LoadedCount = 0;

        // Auto-expand to show photos in the most recent year/month/day
        var mostRecentYear = TimelineGroups.FirstOrDefault();
        if (mostRecentYear != null)
        {
            await ToggleYearAsync(mostRecentYear);
            var mostRecentMonth = mostRecentYear.Months.FirstOrDefault();
            if (mostRecentMonth != null)
            {
                await ToggleMonthAsync(mostRecentMonth);
                var mostRecentDay = mostRecentMonth.Days.FirstOrDefault();
                if (mostRecentDay != null)
                {
                    await ToggleDayAsync(mostRecentDay);
                }
            }
        }

        var allPhotos = GetAllVisiblePhotos();
        EnqueueThumbnails(allPhotos, ThumbnailPriority.High);
    }

    private List<GalleryPhoto> GetAllVisiblePhotos()
    {
        if (IsTimelineView)
        {
            var allPhotos = new List<GalleryPhoto>();
            foreach (var year in TimelineGroups)
            {
                foreach (var month in year.Months)
                {
                    foreach (var day in month.Days)
                    {
                        allPhotos.AddRange(day.Photos);
                    }
                }
            }
            return allPhotos;
        }

        return Photos.ToList();
    }

    private void OnPhotosLoaded(object? sender, IReadOnlyList<GalleryPhoto> e)
    {
        // Handled via LoadInitial/LoadMore commands
    }

    private void OnTotalCountChanged(object? sender, int newCount)
    {
        Application.Current?.Dispatcher.Invoke(() => TotalCount = newCount);
    }

    private void OnMetricsUpdated(object? sender, GalleryMetrics e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            LoadedCount = e.LoadedPhotos;
            MetricsSummary = e.Summary;
            LastQueryLatencyMs = e.LastQueryLatencyMs;
            MemoryEstimate = FormatMemoryEstimate(e.LoadedPhotos);
        });
    }

    private static string FormatMemoryEstimate(int loadedCount)
    {
        const long BytesPerPhoto = 256;
        var totalBytes = (long)loadedCount * BytesPerPhoto;

        return totalBytes switch
        {
            < 1024 * 1024 => $"{totalBytes / 1024.0:F1} KB",
            < 1024L * 1024 * 1024 => $"{totalBytes / (1024.0 * 1024):F1} MB",
            _ => $"{totalBytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }

    private void EnqueueThumbnails(IReadOnlyList<GalleryPhoto> photos, ThumbnailPriority priority)
    {
        foreach (var photo in photos)
        {
            if (photo.IsVideo)
            {
                if (!string.IsNullOrEmpty(photo.VideoThumbnailSmallPath) && File.Exists(photo.VideoThumbnailSmallPath))
                    continue;

                _videoThumbnailWorker.Enqueue(
                    photo.Id,
                    photo.FilePath,
                    photo.ModifiedDateUtc,
                    priority);
            }
            else
            {
            if (!string.IsNullOrEmpty(photo.ThumbnailSmallPath) && File.Exists(photo.ThumbnailSmallPath))
                continue;

            _thumbnailCacheService.Enqueue(
                photo.Id,
                photo.FilePath,
                photo.ModifiedDateUtc,
                priority);
        }
    }
    }

    private void OnThumbnailReady(object? sender, int photoId)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            // Update in flat gallery
            var photo = Photos.FirstOrDefault(p => p.Id == photoId);
            if (photo is not null)
            {
                UpdatePhotoThumbnail(Photos, photo);
                return;
            }

            // Update in timeline view
            foreach (var year in TimelineGroups)
            {
                foreach (var month in year.Months)
                {
                    foreach (var day in month.Days)
                    {
                        var timelinePhoto = day.Photos.FirstOrDefault(p => p.Id == photoId);
                        if (timelinePhoto is not null)
                        {
                            UpdatePhotoThumbnail(day.Photos, timelinePhoto);
                            return;
                        }
                    }
                }
            }
        });
    }

    private void OnVideoThumbnailReady(object? sender, int photoId)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var photo = Photos.FirstOrDefault(p => p.Id == photoId);
            if (photo is not null)
            {
                UpdatePhotoThumbnail(Photos, photo);
                return;
            }

            foreach (var year in TimelineGroups)
            {
                foreach (var month in year.Months)
                {
                    foreach (var day in month.Days)
                    {
                        var timelinePhoto = day.Photos.FirstOrDefault(p => p.Id == photoId);
                        if (timelinePhoto is not null)
                        {
                            UpdatePhotoThumbnail(day.Photos, timelinePhoto);
                            return;
                        }
                    }
                }
            }
        });
    }

    private void UpdatePhotoThumbnail(ObservableCollection<GalleryPhoto> collection, GalleryPhoto photo)
    {
        var index = collection.IndexOf(photo);
        if (index < 0)
            return;

        if (photo.IsVideo)
        {
            var videoThumbnailService = App.Services.GetRequiredService<IVideoThumbnailService>();
            var videoSmallPath = videoThumbnailService.GetThumbnailPath(photo.Id, VideoThumbnailSize.Small);
            var videoMediumPath = videoThumbnailService.GetThumbnailPath(photo.Id, VideoThumbnailSize.Medium);

            var updated = new GalleryPhoto
            {
                Id = photo.Id,
                FilePath = photo.FilePath,
                FileName = photo.FileName,
                Extension = photo.Extension,
                DateTaken = photo.DateTaken,
                Width = photo.Width,
                Height = photo.Height,
                FileSize = photo.FileSize,
                ThumbnailPath = photo.ThumbnailPath,
                ThumbnailSmallPath = photo.ThumbnailSmallPath,
                ThumbnailMediumPath = photo.ThumbnailMediumPath,
                VideoThumbnailSmallPath = videoSmallPath,
                VideoThumbnailMediumPath = videoMediumPath,
                VideoThumbnailLargePath = videoThumbnailService.GetThumbnailPath(photo.Id, VideoThumbnailSize.Large),
                IsFavorite = photo.IsFavorite,
                ModifiedDateUtc = photo.ModifiedDateUtc,
                FolderId = photo.FolderId,
                State = photo.State,
                DateTakenYear = photo.DateTakenYear,
                DateTakenMonth = photo.DateTakenMonth,
                DateTakenDay = photo.DateTakenDay
            };

            collection[index] = updated;
        }
        else
        {
            var thumbnailService = App.Services.GetRequiredService<IThumbnailService>();
            var smallPath = thumbnailService.GetThumbnailPath(photo.Id, ThumbnailSize.Small);
            var mediumPath = thumbnailService.GetThumbnailPath(photo.Id, ThumbnailSize.Medium);

            var updated = new GalleryPhoto
            {
                Id = photo.Id,
                FilePath = photo.FilePath,
                FileName = photo.FileName,
                Extension = photo.Extension,
                DateTaken = photo.DateTaken,
                Width = photo.Width,
                Height = photo.Height,
                FileSize = photo.FileSize,
                ThumbnailPath = photo.ThumbnailPath,
                ThumbnailSmallPath = smallPath,
                ThumbnailMediumPath = mediumPath,
                VideoThumbnailSmallPath = photo.VideoThumbnailSmallPath,
                VideoThumbnailMediumPath = photo.VideoThumbnailMediumPath,
                VideoThumbnailLargePath = photo.VideoThumbnailLargePath,
                IsFavorite = photo.IsFavorite,
                ModifiedDateUtc = photo.ModifiedDateUtc,
                FolderId = photo.FolderId,
                State = photo.State,
                DateTakenYear = photo.DateTakenYear,
                DateTakenMonth = photo.DateTakenMonth,
                DateTakenDay = photo.DateTakenDay
            };

            collection[index] = updated;
        }
    }

    private void OnThumbnailProgressChanged(object? sender, ThumbnailProgress e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ThumbnailStatus = $"Thumbnails: {e.GeneratedCount} generated, {e.CacheSizeBytes / (1024.0 * 1024):F1} MB";
        });
    }

    private void OnLoadingTimerTick(object? sender, EventArgs e)
    {
        LoadingElapsed = $"Loading... {_loadingStopwatch.Elapsed.TotalSeconds:F0}s";
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _loadingTimer.Stop();
        _loadingTimer.Tick -= OnLoadingTimerTick;
        _galleryDataService.PhotosLoaded -= OnPhotosLoaded;
        _galleryDataService.TotalCountChanged -= OnTotalCountChanged;
        _galleryDataService.MetricsUpdated -= OnMetricsUpdated;
        _thumbnailCacheService.ThumbnailReady -= OnThumbnailReady;
        _thumbnailCacheService.ProgressChanged -= OnThumbnailProgressChanged;
        _videoThumbnailWorker.ThumbnailReady -= OnVideoThumbnailReady;
        _ = _videoThumbnailWorker.StopAsync();
        Photos.Clear();
        TimelineGroups.Clear();
    }

    partial void OnTimelineYearCountChanged(int value) => OnPropertyChanged(nameof(TimelineSummary));
    partial void OnTimelineMonthCountChanged(int value) => OnPropertyChanged(nameof(TimelineSummary));
    partial void OnTimelineDayCountChanged(int value) => OnPropertyChanged(nameof(TimelineSummary));
}
