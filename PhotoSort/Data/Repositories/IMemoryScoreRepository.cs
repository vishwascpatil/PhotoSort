using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IMemoryScoreRepository
{
    Task<MemoryScore?> GetAsync(Guid memoryId, int photoId);

    Task<IReadOnlyList<MemoryScore>> GetByMemoryAsync(Guid memoryId);

    Task AddAsync(MemoryScore score);

    Task AddBatchAsync(IReadOnlyList<MemoryScore> scores);

    Task UpdateAsync(MemoryScore score);

    Task DeleteAsync(Guid memoryId, int photoId);

    Task DeleteByMemoryAsync(Guid memoryId);
}
