using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using PhotoSort.Models;
using PhotoSort.Services;
using PhotoSort.ViewModels;

namespace PhotoSort;

public partial class MainWindow : Window
{
    private readonly INavigationService _navigationService;
    private readonly MainViewModel _mainViewModel;
    private readonly Border[] _navItems;

    public MainWindow()
    {
        InitializeComponent();
        _navigationService = App.Services.GetRequiredService<INavigationService>();
        _mainViewModel = App.Services.GetRequiredService<MainViewModel>();
        _mainViewModel.FolderSelected += OnFolderSelected;
        _navItems = [NavAllPhotos, NavMemories, NavPeople, NavTravel, NavSimilar, NavDuplicates, NavCleanup, NavSync];
    }

    private void SelectNav(Border? active)
    {
        var activeBg = (Brush)FindResource("SidebarItemActiveBackground");
        var activeFg = (Brush)FindResource("SidebarItemActiveForeground");
        var inactiveBg = (Brush)FindResource("SidebarItemBackground");
        var inactiveFg = (Brush)FindResource("SidebarItemForeground");

        foreach (var item in _navItems)
        {
            item.Background = item == active ? activeBg : inactiveBg;
            foreach (var tb in FindVisualChildren<TextBlock>(item))
                tb.Foreground = item == active ? activeFg : inactiveFg;
        }
    }

    private void OnFolderSelected(object? sender, SidebarFolderItem? folder)
    {
        SelectNav(null);
    }

    private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
                yield return t;
            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }

    private void OnNavAllPhotos(object sender, MouseButtonEventArgs e)
    {
        _mainViewModel.ClearFolderSelection();
        SelectNav(NavAllPhotos);
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
        _mainViewModel.ClearFolderSelection();
        SelectNav(NavMemories);
        _navigationService.NavigateTo<MemoriesViewModel>();
    }

    private void OnNavPeople(object sender, MouseButtonEventArgs e)
    {
        _mainViewModel.ClearFolderSelection();
        SelectNav(NavPeople);
        _navigationService.NavigateTo<PeopleViewModel>();
    }

    private void OnNavTravel(object sender, MouseButtonEventArgs e)
    {
        _mainViewModel.ClearFolderSelection();
        SelectNav(NavTravel);
        _navigationService.NavigateTo<TravelInsightsViewModel>();
    }

    private void OnNavSimilar(object sender, MouseButtonEventArgs e)
    {
        _mainViewModel.ClearFolderSelection();
        SelectNav(NavSimilar);
        _navigationService.NavigateTo<SimilarPhotosViewModel>();
    }

    private void OnNavDuplicates(object sender, MouseButtonEventArgs e)
    {
        _mainViewModel.ClearFolderSelection();
        SelectNav(NavDuplicates);
        _navigationService.NavigateTo<DuplicateDetectionViewModel>();
    }

    private void OnNavCleanup(object sender, MouseButtonEventArgs e)
    {
        _mainViewModel.ClearFolderSelection();
        SelectNav(NavCleanup);
        _navigationService.NavigateTo<CleanupViewModel>();
    }

    private void OnNavSync(object sender, MouseButtonEventArgs e)
    {
        _mainViewModel.ClearFolderSelection();
        SelectNav(NavSync);
        _navigationService.NavigateTo<SyncViewModel>();
    }
}
