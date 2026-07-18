using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class MemoryFeedbackRepository : Repository<MemoryFeedback>, IMemoryFeedbackRepository
{
    public MemoryFeedbackRepository(IDbContextFactory<PhotoSortDbContext> contextFactory)
        : base(contextFactory)
    {
    }

    public async Task<IReadOnlyList<MemoryFeedback>> GetByMemoryAsync(Guid memoryId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<MemoryFeedback>()
            .Where(f => f.MemoryId == memoryId)
            .OrderByDescending(f => f.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<MemoryFeedback>> GetRecentAsync(int count)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<MemoryFeedback>()
            .OrderByDescending(f => f.CreatedAt)
            .Take(count)
            .AsNoTracking()
            .ToListAsync();
    }
}
