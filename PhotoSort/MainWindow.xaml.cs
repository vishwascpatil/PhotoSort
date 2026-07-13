using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using PhotoSort.Services;
using PhotoSort.ViewModels;

namespace PhotoSort;

public partial class MainWindow : Window
{
    private readonly INavigationService _navigationService;

    public MainWindow()
    {
        InitializeComponent();
        _navigationService = App.Services.GetRequiredService<INavigationService>();
    }

    private void OnNavAllPhotos(object sender, MouseButtonEventArgs e)
    {
        if (_navigationService.CurrentViewModel is GalleryViewModel gallery)
        {
            if (gallery.IsTimelineView)
                gallery.ToggleViewModeCommand.Execute(null);
        }
        else
        {
            _navigationService.NavigateTo<GalleryViewModel>();
        }
    }

    private void OnNavMemories(object sender, MouseButtonEventArgs e)
    {
        if (_navigationService.CurrentViewModel is GalleryViewModel gallery)
        {
            if (!gallery.IsTimelineView)
                gallery.ToggleViewModeCommand.Execute(null);
        }
        else
        {
            _navigationService.NavigateTo<GalleryViewModel>();
            if (_navigationService.CurrentViewModel is GalleryViewModel newGallery)
            {
                if (!newGallery.IsTimelineView)
                    newGallery.ToggleViewModeCommand.Execute(null);
            }
        }
    }

    private void OnNavPeople(object sender, MouseButtonEventArgs e)
    {
        _navigationService.NavigateTo<PeopleViewModel>();
    }

    private void OnNavTravel(object sender, MouseButtonEventArgs e)
    {
        _navigationService.NavigateTo<TravelInsightsViewModel>();
    }

    private void OnNavSimilar(object sender, MouseButtonEventArgs e)
    {
        _navigationService.NavigateTo<SimilarPhotosViewModel>();
    }

    private void OnNavDuplicates(object sender, MouseButtonEventArgs e)
    {
        _navigationService.NavigateTo<DuplicateDetectionViewModel>();
    }

    private void OnNavCleanup(object sender, MouseButtonEventArgs e)
    {
        _navigationService.NavigateTo<CleanupViewModel>();
    }

    private void OnNavSync(object sender, MouseButtonEventArgs e)
    {
        _navigationService.NavigateTo<SyncViewModel>();
    }

    private void OnImportPhotos(object sender, RoutedEventArgs e)
    {
        if (_navigationService.CurrentViewModel is GalleryViewModel gallery)
        {
            gallery.RefreshCommand.Execute(null);
        }
    }
}
