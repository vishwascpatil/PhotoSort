using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PhotoSort.Models;
using PhotoSort.Services;

namespace PhotoSort.ViewModels;

public partial class TravelInsightsViewModel : ObservableObject, IDisposable
{
    private readonly ITravelInsightsService _travelInsightsService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<TravelInsightsViewModel> _logger;

    private bool _disposed;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Loading travel insights...";

    [ObservableProperty]
    private TravelInsights? _insights;

    [ObservableProperty]
    private TravelStatistics? _statistics;

    [ObservableProperty]
    private TravelAnalyticsSummary? _analytics;

    [ObservableProperty]
    private TripSummary? _selectedTrip;

    [ObservableProperty]
    private bool _hasSelectedTrip;

    [ObservableProperty]
    private TravelYear? _selectedYear;

    [ObservableProperty]
    private bool _hasSelectedYear;

    [ObservableProperty]
    private string _renameText = string.Empty;

    [ObservableProperty]
    private bool _isRenameMode;

    public ObservableCollection<TravelYear> Years { get; } = [];
    public ObservableCollection<TravelCountry> TopCountries { get; } = [];
    public ObservableCollection<TravelCity> TopCities { get; } = [];
    public ObservableCollection<TripSummary> RecentTrips { get; } = [];
    public ObservableCollection<TravelAchievement> Achievements { get; } = [];
    public ObservableCollection<MemoryHighlight> MemoryHighlights { get; } = [];
    public ObservableCollection<TravelAnalyticsCard> AnalyticsCards { get; } = [];
    public ObservableCollection<TravelSummaryCard> SummaryCards { get; } = [];

    public TravelInsightsViewModel(
        ITravelInsightsService travelInsightsService,
        INavigationService navigationService,
        ILogger<TravelInsightsViewModel> logger)
    {
        _travelInsightsService = travelInsightsService;
        _navigationService = navigationService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadInsightsAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        StatusMessage = "Loading travel insights...";

        try
        {
            var insights = await _travelInsightsService.GetInsightsAsync();

            Insights = insights;
            Statistics = insights.Statistics;
            Analytics = insights.Analytics;

            Years.Clear();
            foreach (var year in insights.Years)
                Years.Add(year);

            TopCountries.Clear();
            foreach (var country in insights.TopCountries)
                TopCountries.Add(country);

            TopCities.Clear();
            foreach (var city in insights.TopCities)
                TopCities.Add(city);

            RecentTrips.Clear();
            foreach (var trip in insights.RecentTrips)
                RecentTrips.Add(trip);

            Achievements.Clear();
            foreach (var achievement in insights.Achievements)
                Achievements.Add(achievement);

            MemoryHighlights.Clear();
            foreach (var highlight in insights.MemoryHighlights)
                MemoryHighlights.Add(highlight);

            AnalyticsCards.Clear();
            foreach (var card in insights.AnalyticsCards)
                AnalyticsCards.Add(card);

            BuildSummaryCards(insights.Statistics);

            StatusMessage = $"Loaded {insights.Statistics.TripsCompleted} trips across {insights.Statistics.CountriesVisited} countries";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load travel insights");
            StatusMessage = "Failed to load insights";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DetectTripsAsync()
    {
        StatusMessage = "Detecting trips...";
        try
        {
            var count = await _travelInsightsService.DetectTripsAsync();
            StatusMessage = $"Detected {count} new trips";
            await LoadInsightsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect trips");
            StatusMessage = "Failed to detect trips";
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        StatusMessage = "Refreshing...";
        try
        {
            _travelInsightsService.InvalidateCache();
            await LoadInsightsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh");
            StatusMessage = "Failed to refresh";
        }
    }

    [RelayCommand]
    private async Task SelectTripAsync(TripSummary? trip)
    {
        if (trip is null) return;

        SelectedTrip = trip;
        HasSelectedTrip = true;
        RenameText = trip.Name;
        IsRenameMode = false;
    }

    [RelayCommand]
    private void SelectYear(TravelYear? year)
    {
        if (year is null) return;

        SelectedYear = year;
        HasSelectedYear = true;
    }

    [RelayCommand]
    private async Task RenameTripAsync()
    {
        if (SelectedTrip is null || string.IsNullOrWhiteSpace(RenameText))
            return;

        try
        {
            await _travelInsightsService.RenameTripAsync(SelectedTrip.TripId, RenameText);
            IsRenameMode = false;
            await LoadInsightsAsync();
            StatusMessage = $"Renamed to {RenameText}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename trip");
            StatusMessage = "Failed to rename trip";
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteTripAsync()
    {
        if (SelectedTrip is null) return;

        try
        {
            await _travelInsightsService.SetTripFavoriteAsync(SelectedTrip.TripId, !SelectedTrip.IsFavorite);
            await LoadInsightsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle favorite");
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.NavigateTo<GalleryViewModel>();
    }

    private void BuildSummaryCards(TravelStatistics stats)
    {
        SummaryCards.Clear();

        SummaryCards.Add(new TravelSummaryCard
        {
            Icon = "🌍",
            Title = "Countries",
            Value = stats.CountriesVisited.ToString(),
            Subtitle = "visited"
        });

        SummaryCards.Add(new TravelSummaryCard
        {
            Icon = "🗺️",
            Title = "States",
            Value = stats.StatesVisited.ToString(),
            Subtitle = "explored"
        });

        SummaryCards.Add(new TravelSummaryCard
        {
            Icon = "🏙️",
            Title = "Cities",
            Value = stats.CitiesVisited.ToString(),
            Subtitle = "discovered"
        });

        SummaryCards.Add(new TravelSummaryCard
        {
            Icon = "📍",
            Title = "Places",
            Value = stats.PlacesVisited.ToString(),
            Subtitle = "total"
        });

        SummaryCards.Add(new TravelSummaryCard
        {
            Icon = "✈️",
            Title = "Trips",
            Value = stats.TripsCompleted.ToString(),
            Subtitle = "completed"
        });

        SummaryCards.Add(new TravelSummaryCard
        {
            Icon = "📸",
            Title = "Travel Photos",
            Value = stats.TotalPhotosWhileTravelling.ToString("N0"),
            Subtitle = "captured"
        });

        SummaryCards.Add(new TravelSummaryCard
        {
            Icon = "🎬",
            Title = "Travel Videos",
            Value = stats.TotalVideosWhileTravelling.ToString("N0"),
            Subtitle = "recorded"
        });

        SummaryCards.Add(new TravelSummaryCard
        {
            Icon = "📡",
            Title = "GPS Photos",
            Value = stats.GpsEnabledPhotos.ToString("N0"),
            Subtitle = "with coordinates"
        });

        SummaryCards.Add(new TravelSummaryCard
        {
            Icon = "📅",
            Title = "Travel Days",
            Value = stats.TravelDays.ToString(),
            Subtitle = "on the road"
        });

        SummaryCards.Add(new TravelSummaryCard
        {
            Icon = "🛣️",
            Title = "Distance",
            Value = $"{stats.TotalDistanceKm:N0}",
            Subtitle = "km total"
        });

        SummaryCards.Add(new TravelSummaryCard
        {
            Icon = "⏱️",
            Title = "Avg Trip",
            Value = $"{stats.AverageTripDurationDays:F1}",
            Subtitle = "days"
        });

        SummaryCards.Add(new TravelSummaryCard
        {
            Icon = "📷",
            Title = "Avg Photos",
            Value = $"{stats.AveragePhotosPerTrip:F0}",
            Subtitle = "per trip"
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Years.Clear();
        TopCountries.Clear();
        TopCities.Clear();
        RecentTrips.Clear();
        Achievements.Clear();
        MemoryHighlights.Clear();
        AnalyticsCards.Clear();
        SummaryCards.Clear();
    }
}
