using System.Windows;
using System.Windows.Controls;

namespace PhotoSort.Views;

public partial class PhotoTileControl : UserControl
{
    public PhotoTileControl()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    private void ReleaseResources()
    {
        VideoPlayer.Stop();
        VideoPlayer.Source = null;

        VideoPlayer.Visibility = Visibility.Collapsed;
        ThumbnailImage.Visibility = Visibility.Visible;
    }

    private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
    }

    private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
    }

    private void VideoPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        VideoPlayer.Visibility = Visibility.Collapsed;
        ThumbnailImage.Visibility = Visibility.Visible;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ReleaseResources();
        Unloaded -= OnUnloaded;
    }
}
