using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IMemoryStatisticsRepository
{
    Task<MemoryStatistics?> GetByTypeAsync(int memoryTypeId);

    Task<IReadOnlyList<MemoryStatistics>> GetAllAsync();

    Task AddAsync(MemoryStatistics statistics);

    Task UpdateAsync(MemoryStatistics statistics);

    Task AddOrUpdateAsync(MemoryStatistics statistics);
}
