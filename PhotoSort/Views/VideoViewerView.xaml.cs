using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PhotoSort.ViewModels;

namespace PhotoSort.Views;

public partial class VideoViewerView : UserControl
{
    private bool _isPlaying;
    private bool _isOverlayVisible = true;
    private DispatcherTimer? _overlayHideTimer;

    private const int OverlayHideDelayMs = 3000;
    private const double PosterFadeSeconds = 0.4;

    public VideoViewerView()
    {
        InitializeComponent();
    }

    private VideoViewerViewModel? ViewModel => DataContext as VideoViewerViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();
        ShowOverlay();

        var source = ViewModel?.VideoSource;
        if (!string.IsNullOrEmpty(source))
        {
            VideoPlayer.Source = new Uri(source, UriKind.Absolute);
        }

        if (ViewModel is not null)
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _overlayHideTimer?.Stop();

        VideoPlayer.Stop();
        VideoPlayer.Source = null;
        VideoPlayer.Close();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VideoViewerViewModel.IsError) && ViewModel?.IsError == false)
        {
            ErrorOverlay.Visibility = Visibility.Collapsed;
            PosterImage.Visibility = Visibility.Visible;

            var source = ViewModel?.VideoSource;
            if (!string.IsNullOrEmpty(source))
            {
                VideoPlayer.Source = new Uri(source, UriKind.Absolute);
            }
        }
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.IsError == true)
            return;

        var fadeOut = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromSeconds(PosterFadeSeconds),
            FillBehavior = FillBehavior.HoldEnd
        };

        fadeOut.Completed += (_, _) =>
        {
            if (ViewModel is not null && !ViewModel.IsError)
            {
                ViewModel.SetPosterHidden();
            }
        };

        PosterImage.BeginAnimation(OpacityProperty, fadeOut);

        VideoPlayer.Play();
        _isPlaying = true;
        ViewModel?.SetPlayingState(true, false);
        UpdatePlayPauseIcon();
    }

    private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        VideoPlayer.Stop();
        VideoPlayer.Source = null;

        var message = e.ErrorException is not null
            ? $"Failed to load video:\n{e.ErrorException.Message}"
            : "Failed to load video.\nThe file may be missing, corrupted, or in an unsupported format.";

        ViewModel?.SetError(message);

        PosterImage.BeginAnimation(OpacityProperty, null);
        PosterImage.Opacity = 1.0;

        ErrorOverlay.Visibility = Visibility.Visible;
        OverlayPanel.Opacity = 0.0;
        _isOverlayVisible = false;
    }

    private void OnPlayPauseClick(object sender, MouseButtonEventArgs e)
    {
        TogglePlayback();
    }

    private void OnRootPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (ViewModel?.IsError == true)
            return;

        ShowOverlay();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:
                TogglePlayback();
                e.Handled = true;
                break;

            case Key.Escape:
                ViewModel?.CloseCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void TogglePlayback()
    {
        if (ViewModel?.IsError == true)
            return;

        if (_isPlaying)
        {
            VideoPlayer.Pause();
            _isPlaying = false;
            ViewModel?.SetPlayingState(false, true);
        }
        else
        {
            VideoPlayer.Play();
            _isPlaying = true;
            ViewModel?.SetPlayingState(true, false);
        }

        UpdatePlayPauseIcon();

        ShowOverlay();
        if (!_isOverlayVisible)
        {
            ShowOverlay();
        }
        else
        {
            ResetOverlayHideTimer();
        }
    }

    private void UpdatePlayPauseIcon()
    {
        PlayPauseIconText.Text = _isPlaying ? "\u23F8" : "\u25B6";
    }

    private void ShowOverlay()
    {
        if (_isOverlayVisible)
        {
            ResetOverlayHideTimer();
            return;
        }

        _isOverlayVisible = true;

        var fadeIn = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(200)
        };
        OverlayPanel.BeginAnimation(OpacityProperty, fadeIn);

        StartOverlayHideTimer();
    }

    private void HideOverlay()
    {
        if (!_isOverlayVisible)
            return;

        _isOverlayVisible = false;

        var fadeOut = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(500)
        };
        OverlayPanel.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void ResetOverlayHideTimer()
    {
        _overlayHideTimer?.Stop();
        _overlayHideTimer?.Start();
    }

    private void StartOverlayHideTimer()
    {
        if (_overlayHideTimer is null)
        {
            _overlayHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(OverlayHideDelayMs)
            };
            _overlayHideTimer.Tick += OnOverlayHideTick;
        }

        _overlayHideTimer.Stop();
        _overlayHideTimer.Start();
    }

    private void OnOverlayHideTick(object? sender, EventArgs e)
    {
        _overlayHideTimer?.Stop();
        HideOverlay();
    }
}
