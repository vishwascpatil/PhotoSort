using System.Collections.ObjectModel;
using System.Diagnostics;
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
    private readonly IGalleryDataService _galleryDataService;
    private readonly INavigationService _navigationService;
    private readonly IThumbnailService _thumbnailService;
    private readonly ILogger<PhotoViewerViewModel> _logger;

    private List<GalleryPhoto> _allPhotos = [];
    private int _currentIndex = -1;
    private bool _disposed;

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
        IGalleryDataService galleryDataService,
        INavigationService navigationService,
        IThumbnailService thumbnailService,
        ILogger<PhotoViewerViewModel> logger)
    {
        _mediaLoader = mediaLoader;
        _photoRepository = photoRepository;
        _galleryDataService = galleryDataService;
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

        _currentIndex--;
        await LoadCurrentAsync();
    }

    [RelayCommand]
    private async Task GoToNextAsync()
    {
        if (_currentIndex >= _allPhotos.Count - 1 || IsNavigating)
            return;

        _currentIndex++;
        await LoadCurrentAsync();
    }

    [RelayCommand]
    private async Task GoToFirstAsync()
    {
        if (_currentIndex == 0 || IsNavigating)
            return;

        _currentIndex = 0;
        await LoadCurrentAsync();
    }

    [RelayCommand]
    private async Task GoToLastAsync()
    {
        if (_currentIndex >= _allPhotos.Count - 1 || IsNavigating)
            return;

        _currentIndex = _allPhotos.Count - 1;
        await LoadCurrentAsync();
    }

    [RelayCommand]
    private void ToggleMetadata()
    {
        IsMetadataVisible = !IsMetadataVisible;
    }

    [RelayCommand]
    private void Close()
    {
        _mediaLoader.EvictAll();
        _navigationService.NavigateTo<GalleryViewModel>();
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

                // Show thumbnail as instant poster frame while video loads
                var posterPath = CurrentPhoto.ThumbnailMediumPath;
                if (string.IsNullOrEmpty(posterPath) || !File.Exists(posterPath))
                {
                    posterPath = _thumbnailService.GetThumbnailPath(CurrentPhoto.Id, ThumbnailSize.Medium);
                }
                CurrentImage = File.Exists(posterPath) ? LoadThumbnailBitmap(posterPath) : null;
            }
            else if (ImageExtensions.Contains(extension))
            {
                IsVideo = false;
                VideoPath = string.Empty;

                var cached = _mediaLoader.GetCached(CurrentPhoto.Id);
                if (cached is not null)
                {
                    CurrentImage = cached;
                }
                else
                {
                    CurrentImage = await _mediaLoader.LoadImageAsync(CurrentPhoto.FilePath);
                }
            }
            else
            {
                CurrentImage = null;
                IsVideo = false;
                VideoPath = string.Empty;
            }

            PreloadAdjacent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load photo at index {Index}", _currentIndex);
        }
        finally
        {
            IsNavigating = false;
        }
    }

    private void PreloadAdjacent()
    {
        var toPreload = new List<(int Id, string FilePath)>();

        if (_currentIndex > 0)
        {
            var prev = _allPhotos[_currentIndex - 1];
            if (_mediaLoader.GetCached(prev.Id) is null)
                toPreload.Add((prev.Id, prev.FilePath));
        }

        if (_currentIndex < _allPhotos.Count - 1)
        {
            var next = _allPhotos[_currentIndex + 1];
            if (_mediaLoader.GetCached(next.Id) is null)
                toPreload.Add((next.Id, next.FilePath));
        }

        if (toPreload.Count > 0)
        {
            _mediaLoader.PreloadRange(toPreload);
        }

        _mediaLoader.EvictOutside(CurrentPhoto!.Id, radius: 2);
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
            {
                MetadataLocation = $"{fullPhoto.Latitude:F6}, {fullPhoto.Longitude:F6}";
            }
            else
            {
                MetadataLocation = "—";
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to load full metadata for {FilePath}", CurrentPhoto.FilePath);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
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
}
