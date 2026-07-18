using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class MemoryCacheRepository : Repository<MemoryCacheEntry>, IMemoryCacheRepository
{
    public MemoryCacheRepository(IDbContextFactory<PhotoSortDbContext> contextFactory)
        : base(contextFactory)
    {
    }

    public async Task<IReadOnlyList<MemoryCacheEntry>> GetByTypeAsync(string type)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<MemoryCacheEntry>()
            .Where(e => e.Type == type)
            .OrderByDescending(e => e.Score)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<MemoryCacheEntry>> GetExpiredEntriesAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        return await context.Set<MemoryCacheEntry>()
            .Where(e => e.ExpiresAt != null && e.ExpiresAt < now)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<int> DeleteExpiredEntriesAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        return await context.Set<MemoryCacheEntry>()
            .Where(e => e.ExpiresAt != null && e.ExpiresAt < now)
            .ExecuteDeleteAsync();
    }

    public async Task<int> DeleteByTypeAsync(string type)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<MemoryCacheEntry>()
            .Where(e => e.Type == type)
            .ExecuteDeleteAsync();
    }
}
