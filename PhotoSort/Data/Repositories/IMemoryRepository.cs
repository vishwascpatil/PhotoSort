using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IMemoryRepository
{
    Task<Memory> InsertAsync(Memory memory);
    Task UpdateAsync(Memory memory);
    Task<Memory?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<Memory>> GetActiveMemoriesAsync(int count, int offset);
    Task<IReadOnlyList<Memory>> GetMemoriesByDateAsync(DateTime date);
    Task<int> GetActiveCountAsync();
    Task ArchiveStaleAsync(DateTime threshold);
    Task DeleteAsync(Guid id);
    Task<IReadOnlyList<Memory>> GetOnThisDayAsync();
    Task BulkInsertAsync(IReadOnlyList<Memory> memories);
}
