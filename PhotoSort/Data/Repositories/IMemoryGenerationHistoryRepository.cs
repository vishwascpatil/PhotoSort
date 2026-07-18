using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IMemoryGenerationHistoryRepository : IRepository<MemoryGenerationHistory>
{
    Task<IReadOnlyList<MemoryGenerationHistory>> GetByRunAsync(Guid runId);

    Task<IReadOnlyList<MemoryGenerationHistory>> GetRecentAsync(int count);
}
