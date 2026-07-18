using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IMemoryPreferencesRepository : IRepository<MemoryPreferences>
{
    Task<MemoryPreferences?> GetSingleAsync();

    Task<MemoryPreferences> EnsureSingleAsync();
}
