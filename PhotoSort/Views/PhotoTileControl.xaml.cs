using System.IO;
using System.Windows;
using System.Windows.Controls;
using PhotoSort.Models;

namespace PhotoSort.Views;

public partial class PhotoTileControl : UserControl
{
    private bool _isPlaying;
    private bool _clipLoaded;

    public PhotoTileControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += OnIsVisibleChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        StopPlayback();

        if (DataContext is GalleryPhoto photo && photo.IsVideo)
            TryStartPlayback(photo);
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is not GalleryPhoto photo || !photo.IsVideo)
            return;

        if (IsVisible)
            TryStartPlayback(photo);
        else
            StopPlayback();
    }

    private void TryStartPlayback(GalleryPhoto photo)
    {
        if (_isPlaying)
            return;

        var clipPath = photo.PreviewClipPath;
        if (string.IsNullOrEmpty(clipPath) || !File.Exists(clipPath))
            return;

        if (!_clipLoaded)
        {
            VideoPlayer.Source = new Uri(clipPath, UriKind.Absolute);
            _clipLoaded = true;
        }

        StartPlayback();
    }

    private void StartPlayback()
    {
        if (_isPlaying)
            return;

        _isPlaying = true;

        VideoPlayer.Position = TimeSpan.Zero;
        VideoPlayer.Play();
    }

    private void StopPlayback()
    {
        if (!_isPlaying)
            return;

        _isPlaying = false;
        VideoPlayer.Stop();

        VideoPlayer.Visibility = Visibility.Collapsed;
        ThumbnailImage.Visibility = Visibility.Visible;
    }

    private void ReleaseResources()
    {
        _isPlaying = false;

        VideoPlayer.Stop();
        VideoPlayer.Source = null;
        _clipLoaded = false;

        VideoPlayer.Visibility = Visibility.Collapsed;
        ThumbnailImage.Visibility = Visibility.Visible;
    }

    private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            ThumbnailImage.Visibility = Visibility.Collapsed;
            VideoPlayer.Visibility = Visibility.Visible;
        }
    }

    private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            VideoPlayer.Position = TimeSpan.Zero;
            VideoPlayer.Play();
        }
    }

    private void VideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _isPlaying = false;

        VideoPlayer.Visibility = Visibility.Collapsed;
        ThumbnailImage.Visibility = Visibility.Visible;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ReleaseResources();
        DataContextChanged -= OnDataContextChanged;
        IsVisibleChanged -= OnIsVisibleChanged;
        Unloaded -= OnUnloaded;
    }
}
