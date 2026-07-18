using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IMemoryTypeEntityRepository : IRepository<MemoryTypeEntity>
{
    Task<MemoryTypeEntity?> GetByKeyAsync(string key);

    Task<IReadOnlyList<MemoryTypeEntity>> GetActiveByFamilyAsync(int familyId);

    Task<IReadOnlyList<MemoryTypeEntity>> GetAllActiveAsync();
}
