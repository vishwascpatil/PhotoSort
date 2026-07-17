namespace PhotoSort.Services;

public interface INavigationService
{
    void NavigateTo<TViewModel>() where TViewModel : class;
    void NavigateTo(object viewModel);
    object? CurrentViewModel { get; }
    event EventHandler? NavigationChanged;
}
