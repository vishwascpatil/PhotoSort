namespace PhotoSort.Models;

public sealed class TravelInsights
{
    public TravelStatistics Statistics { get; set; } = new();

    public IReadOnlyList<TravelYear> Years { get; set; } = [];

    public IReadOnlyList<TravelCountry> TopCountries { get; set; } = [];

    public IReadOnlyList<TravelCity> TopCities { get; set; } = [];

    public IReadOnlyList<TripSummary> RecentTrips { get; set; } = [];

    public IReadOnlyList<TravelAchievement> Achievements { get; set; } = [];

    public IReadOnlyList<MemoryHighlight> MemoryHighlights { get; set; } = [];

    public IReadOnlyList<TravelAnalyticsCard> AnalyticsCards { get; set; } = [];

    public TravelAnalyticsSummary Analytics { get; set; } = new();
}
