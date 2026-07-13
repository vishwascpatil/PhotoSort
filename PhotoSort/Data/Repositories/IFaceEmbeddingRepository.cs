using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IFaceEmbeddingRepository : IRepository<FaceEmbedding>
{
    Task<FaceEmbedding?> GetByFaceIdAsync(int faceId);
    Task<IReadOnlyList<FaceEmbedding>> GetByFaceIdsAsync(IEnumerable<int> faceIds);
    Task<IReadOnlyList<FaceEmbedding>> GetEmbeddingsForPersonAsync(int personId);
    Task<IReadOnlyList<FaceEmbedding>> GetUnassignedEmbeddingsAsync(int limit = 1000);
    Task<int> GetEmbeddingCountAsync();
    Task<float[]?> GetCentroidForPersonAsync(int personId);
    Task<IReadOnlyList<(int PersonId, float[] Centroid)>> GetAllPersonCentroidsAsync();
}
