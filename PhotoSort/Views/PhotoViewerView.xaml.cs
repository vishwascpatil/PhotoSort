using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PhotoSort.ViewModels;

namespace PhotoSort.Views;

public partial class PhotoViewerView : UserControl
{
    private bool _isPanning;
    private Point _panStart;
    private bool _isFirstLoad = true;

    public PhotoViewerView()
    {
        InitializeComponent();
    }

    private PhotoViewerViewModel? ViewModel => DataContext as PhotoViewerViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();

        if (_isFirstLoad && ViewModel is not null)
        {
            _isFirstLoad = false;
            FitImageToScreen();
        }

        ViewModel!.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        VideoPlayer?.Close();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.CurrentImage))
        {
            Dispatcher.BeginInvoke(() => FitImageToScreen());
        }
        else if (e.PropertyName == nameof(ViewModel.VideoPath))
        {
            var path = ViewModel?.VideoPath;
            if (ViewModel?.IsVideo == true && !string.IsNullOrEmpty(path))
            {
                ImageCanvas.Visibility = Visibility.Visible;
                VideoPlayer.Visibility = Visibility.Collapsed;
                VideoPlayer.Source = new Uri(path, UriKind.Absolute);
                VideoPlayer.Play();
            }
            else
            {
                VideoPlayer.Stop();
                VideoPlayer.Source = null;
                ImageCanvas.Visibility = Visibility.Visible;
            }
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var vm = ViewModel;
        if (vm is null)
            return;

        switch (e.Key)
        {
            case Key.Left:
                vm.GoToPreviousCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Right:
                vm.GoToNextCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Space:
                ToggleVideoPlayback();
                e.Handled = true;
                break;

            case Key.F:
                ResetTransform();
                e.Handled = true;
                break;

            case Key.Escape:
                vm.CloseCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Add:
            case Key.OemPlus:
                vm.ZoomInCommand.Execute(null);
                UpdateImageTransform();
                e.Handled = true;
                break;

            case Key.Subtract:
            case Key.OemMinus:
                vm.ZoomOutCommand.Execute(null);
                UpdateImageTransform();
                e.Handled = true;
                break;

            case Key.M:
                vm.ToggleMetadataCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnPreviousClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.GoToPreviousCommand.Execute(null);
    }

    private void OnNextClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.GoToNextCommand.Execute(null);
    }

    private void OnContentMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var vm = ViewModel;
        if (vm is null)
            return;

        vm.ApplyZoom(e.Delta > 0 ? 1 : -1);
        UpdateImageTransform();
    }

    private void OnContentMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel?.ZoomLevel > 1.0)
        {
            _isPanning = true;
            _panStart = e.GetPosition(ImageCanvas);
            ImageCanvas.CaptureMouse();
            ImageCanvas.Cursor = Cursors.Hand;
        }
    }

    private void OnContentMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            ImageCanvas.ReleaseMouseCapture();
            ImageCanvas.Cursor = Cursors.Arrow;
        }
    }

    private void OnContentMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning)
            return;

        var currentPos = e.GetPosition(ImageCanvas);
        var dx = currentPos.X - _panStart.X;
        var dy = currentPos.Y - _panStart.Y;

        ImageTranslateTransform.X += dx;
        ImageTranslateTransform.Y += dy;

        _panStart = currentPos;
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.IsVideo == true)
        {
            ImageCanvas.Visibility = Visibility.Collapsed;
            VideoPlayer.Visibility = Visibility.Visible;
            VideoPlayer.Play();
        }
    }

    private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Media failed: {e.ErrorException}");
    }

    private void ToggleVideoPlayback()
    {
        if (ViewModel?.IsVideo != true)
            return;

        if (VideoPlayer.Position >= VideoPlayer.NaturalDuration.TimeSpan)
        {
            VideoPlayer.Position = TimeSpan.Zero;
            VideoPlayer.Play();
        }
        else if (VideoPlayer.SpeedRatio > 0)
        {
            VideoPlayer.Pause();
        }
        else
        {
            VideoPlayer.Play();
        }
    }

    private void FitImageToScreen()
    {
        if (ViewModel?.CurrentImage is null || ViewModel.IsVideo)
            return;

        var containerWidth = ImageCanvas.ActualWidth;
        var containerHeight = ImageCanvas.ActualHeight;
        if (containerWidth <= 0 || containerHeight <= 0)
            return;

        var imageWidth = ViewModel.CurrentImage.PixelWidth;
        var imageHeight = ViewModel.CurrentImage.PixelHeight;
        if (imageWidth <= 0 || imageHeight <= 0)
            return;

        var scaleX = containerWidth / imageWidth;
        var scaleY = containerHeight / imageHeight;
        var scale = Math.Min(scaleX, scaleY);

        ViewModel.ZoomLevel = scale;
        ViewModel.ZoomText = $"{(scale * 100):F0}%";
        UpdateImageTransform();
    }

    private void UpdateImageTransform()
    {
        if (ViewModel is null)
            return;

        var scale = ViewModel.ZoomLevel;
        ImageScaleTransform.ScaleX = scale;
        ImageScaleTransform.ScaleY = scale;

        if (scale <= 1.0)
        {
            ImageTranslateTransform.X = 0;
            ImageTranslateTransform.Y = 0;
        }
    }

    private void ResetTransform()
    {
        ImageScaleTransform.ScaleX = 1.0;
        ImageScaleTransform.ScaleY = 1.0;
        ImageTranslateTransform.X = 0;
        ImageTranslateTransform.Y = 0;
    }
}
