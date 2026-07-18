using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IMemoryCacheRepository : IRepository<MemoryCacheEntry>
{
    Task<IReadOnlyList<MemoryCacheEntry>> GetByTypeAsync(string type);

    Task<IReadOnlyList<MemoryCacheEntry>> GetExpiredEntriesAsync();

    Task<int> DeleteExpiredEntriesAsync();

    Task<int> DeleteByTypeAsync(string type);
}
