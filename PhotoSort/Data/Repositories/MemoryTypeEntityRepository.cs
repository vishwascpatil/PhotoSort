using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class MemoryTypeEntityRepository : Repository<MemoryTypeEntity>, IMemoryTypeEntityRepository
{
    public MemoryTypeEntityRepository(IDbContextFactory<PhotoSortDbContext> contextFactory)
        : base(contextFactory)
    {
    }

    public async Task<MemoryTypeEntity?> GetByKeyAsync(string key)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<MemoryTypeEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Key == key);
    }

    public async Task<IReadOnlyList<MemoryTypeEntity>> GetActiveByFamilyAsync(int familyId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<MemoryTypeEntity>()
            .Where(t => t.FamilyId == familyId && t.IsActive)
            .OrderBy(t => t.SortOrder)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<MemoryTypeEntity>> GetAllActiveAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<MemoryTypeEntity>()
            .Include(t => t.Family)
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .AsNoTracking()
            .ToListAsync();
    }
}
