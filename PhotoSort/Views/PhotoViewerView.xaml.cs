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
            ViewModel.FitToScreenCommand.Execute(null);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        VideoPlayer?.Close();
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
                vm.FitToScreenCommand.Execute(null);
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
            VideoPlayer.Play();
        }
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
