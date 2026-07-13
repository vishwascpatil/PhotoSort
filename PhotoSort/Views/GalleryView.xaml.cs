using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PhotoSort.Models;
using PhotoSort.ViewModels;

namespace PhotoSort.Views;

public partial class GalleryView : UserControl
{
    private readonly DispatcherTimer _scrollDebounceTimer;
    private readonly Stopwatch _scrollStopwatch;
    private bool _isFirstLoad = true;
    private bool _isHandlingScroll;

    private const double ScrollThreshold = 0.85;
    private const int ScrollDebounceMs = 100;

    public GalleryView()
    {
        InitializeComponent();

        _scrollDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(ScrollDebounceMs)
        };
        _scrollDebounceTimer.Tick += OnScrollDebounceTick;

        _scrollStopwatch = new Stopwatch();

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private GalleryViewModel? ViewModel => DataContext as GalleryViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isFirstLoad)
        {
            _isFirstLoad = false;

            if (ViewModel is not null)
            {
                _ = ViewModel.LoadInitialCommand.ExecuteAsync(null);
            }
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ViewModel is not null && ActualWidth > 0 && ActualHeight > 0)
        {
            ViewModel.OnViewportChanged((int)ActualWidth, (int)ActualHeight);
        }
    }

    private void ScrollViewer_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollChanged += OnScrollChanged;
        }
    }

    private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
            return;

        UpdateLoadingOverlay(scrollViewer);
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isHandlingScroll)
            return;

        if (sender is not ScrollViewer scrollViewer)
            return;

        if (!scrollViewer.CanContentScroll)
            return;

        if (ViewModel?.GetIsLoading() == true)
            return;

        var verticalOffset = scrollViewer.VerticalOffset;
        var extentHeight = scrollViewer.ExtentHeight;
        var viewportHeight = scrollViewer.ViewportHeight;

        if (extentHeight <= 0 || viewportHeight <= 0)
            return;

        var scrollPercentage = (verticalOffset + viewportHeight) / extentHeight;

        if (scrollPercentage >= ScrollThreshold)
        {
            _scrollStopwatch.Restart();
            _scrollDebounceTimer.Stop();
            _scrollDebounceTimer.Start();
        }
    }

    private void OnScrollDebounceTick(object? sender, EventArgs e)
    {
        _scrollDebounceTimer.Stop();
        _scrollStopwatch.Stop();

        if (ViewModel is null || ViewModel.GetIsLoading() || !ViewModel.HasMorePages)
            return;

        _isHandlingScroll = true;

        try
        {
            _ = ViewModel.LoadMoreCommand.ExecuteAsync(null);
        }
        finally
        {
            _isHandlingScroll = false;
        }
    }

    private void UpdateLoadingOverlay(ScrollViewer scrollViewer)
    {
        if (LoadingOverlay is null)
            return;

        var isLoading = ViewModel?.GetIsLoading() == true;
        var hasNoPhotos = ViewModel?.IsGalleryEmpty == true;

        LoadingOverlay.Visibility = isLoading && !hasNoPhotos
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);

        if (oldParent is null && DataContext is GalleryViewModel vm)
        {
            vm.Photos.CollectionChanged += OnPhotosCollectionChanged;
        }
    }

    private void OnPhotosCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (GalleryScrollViewer is not null)
        {
            UpdateLoadingOverlay(GalleryScrollViewer);
        }
    }

    private void ScrollViewer_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollChanged -= OnScrollChanged;
        }

        _scrollDebounceTimer.Stop();
        _scrollDebounceTimer.Tick -= OnScrollDebounceTick;

        if (ViewModel is not null)
        {
            ViewModel.Photos.CollectionChanged -= OnPhotosCollectionChanged;
        }

        Loaded -= OnLoaded;
        SizeChanged -= OnSizeChanged;
    }

    private void OnPhotoTileDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is GalleryPhoto photo)
        {
            ViewModel?.OpenPhotoCommand.Execute(photo.Id);
        }
    }

    private void OnTimelinePhotoDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is GalleryPhoto photo)
        {
            ViewModel?.OpenPhotoCommand.Execute(photo.Id);
        }
    }
}
