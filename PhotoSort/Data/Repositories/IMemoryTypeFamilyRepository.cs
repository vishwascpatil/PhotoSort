using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IMemoryTypeFamilyRepository : IRepository<MemoryTypeFamily>
{
    Task<MemoryTypeFamily?> GetByNameAsync(string name);

    Task<IReadOnlyList<MemoryTypeFamily>> GetAllWithTypesAsync();
}
