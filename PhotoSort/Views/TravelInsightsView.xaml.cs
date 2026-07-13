using System.Windows.Input;
using PhotoSort.Models;
using PhotoSort.ViewModels;

namespace PhotoSort.Views;

public partial class TravelInsightsView : System.Windows.Controls.UserControl
{
    public TravelInsightsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is TravelInsightsViewModel viewModel)
        {
            await viewModel.LoadInsightsCommand.ExecuteAsync(null);
        }
    }

    private void OnYearClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement element && element.DataContext is TravelYear year
            && DataContext is TravelInsightsViewModel viewModel)
        {
            viewModel.SelectYearCommand.Execute(year);
        }
    }

    private void OnTripClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement element && element.DataContext is TripSummary trip
            && DataContext is TravelInsightsViewModel viewModel)
        {
            viewModel.SelectTripCommand.Execute(trip);
        }
    }
}
