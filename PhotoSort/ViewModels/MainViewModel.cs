using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoSort.Services;

namespace PhotoSort.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;

    public MainViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _navigationService.NavigationChanged += OnNavigationChanged;

        _navigationService.NavigateTo<WelcomeViewModel>();
    }

    [ObservableProperty]
    private object? _currentViewModel;

    [RelayCommand]
    private void NavigateToWelcome()
    {
        _navigationService.NavigateTo<WelcomeViewModel>();
    }

    private void OnNavigationChanged(object? sender, EventArgs e)
    {
        CurrentViewModel = _navigationService.CurrentViewModel;
    }
}
