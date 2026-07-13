using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IPlaceRepository : IRepository<Place>
{
    Task<Place?> GetByNameAsync(string name);
    Task<IReadOnlyList<Place>> GetAllWithPhotoCountAsync();
    Task<int> GetTotalPlaceCountAsync();
    Task<IReadOnlyList<Place>> GetPlacesWithGpsAsync();
    Task<IReadOnlyList<(int PlaceId, int PhotoCount, DateTime? FirstDate, DateTime? LastDate)>> GetPlaceStatsAsync();
}
