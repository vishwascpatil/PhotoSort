using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoSort.Models;
using PhotoSort.Services;

namespace PhotoSort.ViewModels;

public partial class VideoViewerViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _videoSource = string.Empty;

    [ObservableProperty]
    private BitmapImage? _posterSource;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isPosterVisible = true;

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public VideoViewerViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public void Initialize(GalleryPhoto photo)
    {
        VideoSource = photo.FilePath;
        FileName = photo.FileName;
        PosterSource = LoadBestPoster(photo);
        IsPosterVisible = PosterSource is not null;
    }

    [RelayCommand]
    private void Close()
    {
        _navigationService.NavigateTo<GalleryViewModel>();
    }

    [RelayCommand]
    private void Retry()
    {
        IsError = false;
        ErrorMessage = string.Empty;
    }

    public void SetPlayingState(bool playing, bool paused)
    {
        IsPlaying = playing;
        IsPaused = paused;
    }

    public void SetPosterHidden()
    {
        IsPosterVisible = false;
    }

    public void SetError(string message)
    {
        IsError = true;
        ErrorMessage = message;
        IsPlaying = false;
    }

    private static BitmapImage? LoadBestPoster(GalleryPhoto photo)
    {
        var path = photo.VideoThumbnailLargePath
            ?? photo.VideoThumbnailMediumPath
            ?? photo.VideoThumbnailSmallPath
            ?? photo.EffectiveMediumThumbnail
            ?? photo.ThumbnailPath;

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

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
}
