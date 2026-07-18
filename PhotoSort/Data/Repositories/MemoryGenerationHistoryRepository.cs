using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class MemoryGenerationHistoryRepository : Repository<MemoryGenerationHistory>, IMemoryGenerationHistoryRepository
{
    public MemoryGenerationHistoryRepository(IDbContextFactory<PhotoSortDbContext> contextFactory)
        : base(contextFactory)
    {
    }

    public async Task<IReadOnlyList<MemoryGenerationHistory>> GetByRunAsync(Guid runId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<MemoryGenerationHistory>()
            .Where(h => h.RunId == runId)
            .OrderBy(h => h.StartedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<MemoryGenerationHistory>> GetRecentAsync(int count)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<MemoryGenerationHistory>()
            .OrderByDescending(h => h.StartedAt)
            .Take(count)
            .AsNoTracking()
            .ToListAsync();
    }
}
