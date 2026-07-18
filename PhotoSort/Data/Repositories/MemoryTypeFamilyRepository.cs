using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class MemoryTypeFamilyRepository : Repository<MemoryTypeFamily>, IMemoryTypeFamilyRepository
{
    public MemoryTypeFamilyRepository(IDbContextFactory<PhotoSortDbContext> contextFactory)
        : base(contextFactory)
    {
    }

    public async Task<MemoryTypeFamily?> GetByNameAsync(string name)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<MemoryTypeFamily>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Name == name);
    }

    public async Task<IReadOnlyList<MemoryTypeFamily>> GetAllWithTypesAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<MemoryTypeFamily>()
            .Include(f => f.MemoryTypes)
            .OrderBy(f => f.SortOrder)
            .AsNoTracking()
            .ToListAsync();
    }
}
