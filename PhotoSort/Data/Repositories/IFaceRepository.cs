using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IFaceRepository : IRepository<Face>
{
    Task<IReadOnlyList<Face>> GetByPhotoIdAsync(int photoId);
    Task<IReadOnlyList<Face>> GetByPersonIdAsync(int personId);
    Task<IReadOnlyList<Face>> GetUnassignedFacesAsync(int limit = 1000);
    Task<IReadOnlyList<Face>> GetFacesWithEmbeddingsAsync(int limit = 1000);
    Task<int> GetFaceCountByPersonIdAsync(int personId);
    Task<int> GetTotalFaceCountAsync();
    Task<int> GetAssignedFaceCountAsync();
    Task<Face?> GetWithDetailsAsync(int faceId);
    Task<IReadOnlyList<Face>> GetIgnoredFacesAsync();
    Task<int> GetIgnoredFaceCountAsync();
    Task<int> GetFaceCountForPhotoAsync(int photoId);
}
