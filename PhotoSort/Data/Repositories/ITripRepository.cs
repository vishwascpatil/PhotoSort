using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface ITripRepository : IRepository<Trip>
{
    Task<IReadOnlyList<Trip>> GetAllWithDetailsAsync();
    Task<Trip?> GetWithDetailsAsync(int tripId);
    Task<IReadOnlyList<TripPhoto>> GetTripPhotosAsync(int tripId);
    Task<IReadOnlyList<TripPlace>> GetTripPlacesAsync(int tripId);
    Task<int> GetTotalTripCountAsync();
    Task<IReadOnlyList<Trip>> GetTripsByDateRangeAsync(DateTime start, DateTime end);
    Task<IReadOnlyList<Trip>> GetFavoriteTripsAsync();
    Task<Trip?> GetTripForPhotoAsync(int photoId);
    Task<bool> PhotoHasTripAsync(int photoId);
}
