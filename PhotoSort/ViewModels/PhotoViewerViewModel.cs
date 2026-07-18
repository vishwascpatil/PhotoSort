using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services;

namespace PhotoSort.ViewModels;

public partial class PhotoViewerViewModel : ObservableObject, IDisposable
{
    private readonly IMediaLoaderService _mediaLoader;
    private readonly IPhotoRepository _photoRepository;
    private readonly INavigationService _navigationService;
    private readonly IThumbnailService _thumbnailService;
    private readonly ILogger<PhotoViewerViewModel> _logger;

    private List<GalleryPhoto> _allPhotos = [];
    private int _currentIndex = -1;
    private CancellationTokenSource? _loadCts;
    private bool _disposed;

    private const int PreloadRadius = 2;
    private const int LookAheadWindow = 2;

    [ObservableProperty]
    private BitmapImage? _currentImage;

    [ObservableProperty]
    private GalleryPhoto? _currentPhoto;

    [ObservableProperty]
    private bool _isVideo;

    [ObservableProperty]
    private string _videoPath = string.Empty;

    [ObservableProperty]
    private bool _isNavigating;

    [ObservableProperty]
    private bool _isMetadataVisible = true;

    [ObservableProperty]
    private string _positionText = "0 / 0";

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private string _zoomText = "Fit";

    [ObservableProperty]
    private string _isHighResLoaded = "False";

    [ObservableProperty]
    private string _metadataDateTaken = string.Empty;

    [ObservableProperty]
    private string _metadataFileName = string.Empty;

    [ObservableProperty]
    private string _metadataFileSize = string.Empty;

    [ObservableProperty]
    private string _metadataDimensions = string.Empty;

    [ObservableProperty]
    private string _metadataCameraMake = string.Empty;

    [ObservableProperty]
    private string _metadataCameraModel = string.Empty;

    [ObservableProperty]
    private string _metadataLocation = string.Empty;

    private static readonly HashSet<string> ImageExtensions =
    [
        ".jpg", ".jpeg", ".png", ".heic", ".webp", ".bmp", ".gif", ".tiff", ".tif"
    ];

    private static readonly HashSet<string> VideoExtensions =
    [
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".webm", ".m4v"
    ];

    public bool HasPrevious => _currentIndex > 0;
    public bool HasNext => _currentIndex < _allPhotos.Count - 1;

    public PhotoViewerViewModel(
        IMediaLoaderService mediaLoader,
        IPhotoRepository photoRepository,
        INavigationService navigationService,
        IThumbnailService thumbnailService,
        ILogger<PhotoViewerViewModel> logger)
    {
        _mediaLoader = mediaLoader;
        _photoRepository = photoRepository;
        _navigationService = navigationService;
        _thumbnailService = thumbnailService;
        _logger = logger;
    }

    public void Initialize(IReadOnlyList<GalleryPhoto> photos, int startIndex)
    {
        _allPhotos = photos.ToList();
        _currentIndex = Math.Clamp(startIndex, 0, Math.Max(0, _allPhotos.Count - 1));

        _ = LoadCurrentAsync();
    }

    [RelayCommand]
    private async Task GoToPreviousAsync()
    {
        if (_currentIndex <= 0 || IsNavigating)
            return;

        CancelPendingLoad();
        _currentIndex--;
        await LoadCurrentAsync();
    }

    [RelayCommand]
    private async Task GoToNextAsync()
    {
        if (_currentIndex >= _allPhotos.Count - 1 || IsNavigating)
            return;

        CancelPendingLoad();
        _currentIndex++;
        await LoadCurrentAsync();
    }

    [RelayCommand]
    private void Close()
    {
        CancelPendingLoad();
        CurrentImage = null;
        _mediaLoader.EvictAll();
        _navigationService.NavigateTo<GalleryViewModel>();
    }

    [RelayCommand]
    private void ToggleMetadata()
    {
        IsMetadataVisible = !IsMetadataVisible;
    }

    [RelayCommand]
    private void FitToScreen()
    {
        ZoomLevel = 1.0;
        ZoomText = "Fit";
    }

    [RelayCommand]
    private void ActualSize()
    {
        if (CurrentPhoto?.Width > 0)
        {
            ZoomLevel = 1.0;
            ZoomText = "100%";
        }
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(ZoomLevel * 1.25, 10.0);
        ZoomText = $"{ZoomLevel * 100:F0}%";
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(ZoomLevel / 1.25, 0.1);
        ZoomText = $"{ZoomLevel * 100:F0}%";
    }

    public void ApplyZoom(double delta)
    {
        if (delta > 0)
            ZoomLevel = Math.Min(ZoomLevel * 1.1, 10.0);
        else
            ZoomLevel = Math.Max(ZoomLevel / 1.1, 0.1);

        ZoomText = $"{ZoomLevel * 100:F0}%";
    }

    private async Task LoadCurrentAsync()
    {
        if (_currentIndex < 0 || _currentIndex >= _allPhotos.Count)
            return;

        IsNavigating = true;
        IsHighResLoaded = "False";

        var cts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _loadCts, cts);
        oldCts?.Cancel();
        oldCts?.Dispose();

        var ct = cts.Token;

        try
        {
            CurrentPhoto = _allPhotos[_currentIndex];
            PositionText = $"{_currentIndex + 1} / {_allPhotos.Count}";

            OnPropertyChanged(nameof(HasPrevious));
            OnPropertyChanged(nameof(HasNext));

            UpdateMetadata();

            var extension = Path.GetExtension(CurrentPhoto.FilePath).ToLowerInvariant();

            if (VideoExtensions.Contains(extension))
            {
                IsVideo = true;
                VideoPath = CurrentPhoto.FilePath;

                var posterPath = GetThumbnailPath(CurrentPhoto);
                CurrentImage = !string.IsNullOrEmpty(posterPath) && File.Exists(posterPath)
                    ? LoadThumbnailBitmap(posterPath)
                    : null;
            }
            else if (ImageExtensions.Contains(extension))
            {
                IsVideo = false;
                VideoPath = string.Empty;

                // 1. Instant thumbnail from local cache (512px JPEG)
                var thumbnailPath = GetThumbnailPath(CurrentPhoto);
                var thumbnailImage = !string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath)
                    ? LoadThumbnailBitmap(thumbnailPath)
                    : null;
                CurrentImage = thumbnailImage;

                // 2. Check LRU cache for preloaded full-res
                var cached = _mediaLoader.GetCached(CurrentPhoto.Id);
                if (cached is not null)
                {
                    CurrentImage = cached;
                    IsHighResLoaded = "True";
                }
                else
                {
                    // 3. Background decode full-res (Task.Run + DecodePixelWidth + Freeze)
                    _ = LoadFullResolutionAsync(CurrentPhoto.Id, CurrentPhoto.FilePath, ct);
                }
            }
            else
            {
                CurrentImage = null;
                IsVideo = false;
                VideoPath = string.Empty;
            }

            // 4. Fill look-ahead window in LRU cache
            PreloadLookAhead();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load photo at index {Index}", _currentIndex);
        }
        finally
        {
            IsNavigating = false;
        }
    }

    private async Task LoadFullResolutionAsync(int photoId, string filePath, CancellationToken ct)
    {
        try
        {
            var fullImage = await _mediaLoader.LoadImageAsync(photoId, filePath, ct);

            if (ct.IsCancellationRequested)
                return;

            if (fullImage is not null && _currentIndex >= 0 && _currentIndex < _allPhotos.Count)
            {
                var currentId = _allPhotos[_currentIndex].Id;
                if (currentId == photoId)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!ct.IsCancellationRequested)
                        {
                            CurrentImage = fullImage;
                            IsHighResLoaded = "True";
                        }
                    });
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to load full resolution for photo {PhotoId}", photoId);
        }
    }

    private void PreloadLookAhead()
    {
        var currentId = CurrentPhoto?.Id;
        if (currentId is null)
            return;

        var toPreload = new List<(int Id, string FilePath)>();

        for (int offset = 1; offset <= LookAheadWindow; offset++)
        {
            var prevIdx = _currentIndex - offset;
            if (prevIdx >= 0)
            {
                var prev = _allPhotos[prevIdx];
                if (_mediaLoader.GetCached(prev.Id) is null && IsImageFile(prev.FilePath))
                    toPreload.Add((prev.Id, prev.FilePath));
            }

            var nextIdx = _currentIndex + offset;
            if (nextIdx < _allPhotos.Count)
            {
                var next = _allPhotos[nextIdx];
                if (_mediaLoader.GetCached(next.Id) is null && IsImageFile(next.FilePath))
                    toPreload.Add((next.Id, next.FilePath));
            }
        }

        foreach (var (id, path) in toPreload)
        {
            _ = LoadFullResolutionAsync(id, path, CancellationToken.None);
        }

        _mediaLoader.EvictOutside(currentId.Value, PreloadRadius);
    }

    private void UpdateMetadata()
    {
        if (CurrentPhoto is null)
            return;

        MetadataFileName = CurrentPhoto.FileName;
        MetadataFileSize = CurrentPhoto.DisplaySize;
        MetadataDateTaken = CurrentPhoto.DisplayDate;

        if (CurrentPhoto.Width > 0 && CurrentPhoto.Height > 0)
            MetadataDimensions = $"{CurrentPhoto.Width} × {CurrentPhoto.Height}";
        else
            MetadataDimensions = "Unknown";

        MetadataCameraMake = "—";
        MetadataCameraModel = "—";
        MetadataLocation = "—";

        _ = LoadFullMetadataAsync();
    }

    private async Task LoadFullMetadataAsync()
    {
        if (CurrentPhoto is null)
            return;

        try
        {
            var fullPhoto = await _photoRepository.GetByFilePathAsync(CurrentPhoto.FilePath);
            if (fullPhoto is null || fullPhoto.FilePath != CurrentPhoto.FilePath)
                return;

            MetadataCameraMake = string.IsNullOrWhiteSpace(fullPhoto.CameraMake) ? "—" : fullPhoto.CameraMake;
            MetadataCameraModel = string.IsNullOrWhiteSpace(fullPhoto.CameraModel) ? "—" : fullPhoto.CameraModel;

            if (fullPhoto.Latitude.HasValue && fullPhoto.Longitude.HasValue)
                MetadataLocation = $"{fullPhoto.Latitude:F6}, {fullPhoto.Longitude:F6}";
            else
                MetadataLocation = "—";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to load full metadata for {FilePath}", CurrentPhoto.FilePath);
        }
    }

    private string? GetThumbnailPath(GalleryPhoto photo)
    {
        var path = photo.EffectiveMediumThumbnail;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            return path;

        try
        {
            path = _thumbnailService.GetThumbnailPath(photo.Id, ThumbnailSize.Medium);
            if (File.Exists(path))
                return path;
        }
        catch { }

        return photo.ThumbnailSmallPath;
    }

    private void CancelPendingLoad()
    {
        var cts = Interlocked.Exchange(ref _loadCts, null);
        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CancelPendingLoad();
        CurrentImage = null;
        _mediaLoader.EvictAll();
    }

    private static BitmapImage? LoadThumbnailBitmap(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.None;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsImageFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ImageExtensions.Contains(ext);
    }
}
