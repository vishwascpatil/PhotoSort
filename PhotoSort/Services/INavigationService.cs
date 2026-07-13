namespace PhotoSort.Services;

public interface INavigationService
{
    void NavigateTo<TViewModel>() where TViewModel : class;
    object? CurrentViewModel { get; }
    event EventHandler? NavigationChanged;
}
