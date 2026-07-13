using Microsoft.Extensions.DependencyInjection;

namespace PhotoSort.Services;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public object? CurrentViewModel { get; private set; }

    public event EventHandler? NavigationChanged;

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        CurrentViewModel = viewModel;
        NavigationChanged?.Invoke(this, EventArgs.Empty);
    }
}
