using PhotoSort.Models;

namespace PhotoSort.Services;

public interface ITravelInsightsService : IDisposable
{
    Task<TravelInsights> GetInsightsAsync(CancellationToken cancellationToken = default);

    Task<TravelStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TravelYear>> GetTravelYearsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TravelCountry>> GetTopCountriesAsync(int limit = 10, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TravelCity>> GetTopCitiesAsync(int limit = 10, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TripSummary>> GetRecentTripsAsync(int limit = 20, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TravelAchievement>> GetAchievementsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemoryHighlight>> GetMemoryHighlightsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TravelAnalyticsCard>> GetAnalyticsCardsAsync(CancellationToken cancellationToken = default);

    Task<TripSummary?> GetTripSummaryAsync(int tripId, CancellationToken cancellationToken = default);

    Task<Trip> RenameTripAsync(int tripId, string newName, CancellationToken cancellationToken = default);

    Task<Trip> SetTripFavoriteAsync(int tripId, bool isFavorite, CancellationToken cancellationToken = default);

    Task<Trip> SetTripNotesAsync(int tripId, string? notes, CancellationToken cancellationToken = default);

    Task<Trip> SetTripCoverPhotoAsync(int tripId, int photoId, CancellationToken cancellationToken = default);

    Task<int> DetectTripsAsync(CancellationToken cancellationToken = default);

    Task RefreshAsync(CancellationToken cancellationToken = default);

    void InvalidateCache();
}
