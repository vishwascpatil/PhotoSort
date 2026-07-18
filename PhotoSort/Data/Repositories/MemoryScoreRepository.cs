using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class MemoryScoreRepository : IMemoryScoreRepository
{
    private readonly IDbContextFactory<PhotoSortDbContext> _contextFactory;

    public MemoryScoreRepository(IDbContextFactory<PhotoSortDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<MemoryScore?> GetAsync(Guid memoryId, int photoId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Set<MemoryScore>().FindAsync(memoryId, photoId);
    }

    public async Task<IReadOnlyList<MemoryScore>> GetByMemoryAsync(Guid memoryId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Set<MemoryScore>()
            .Where(s => s.MemoryId == memoryId)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddAsync(MemoryScore score)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        score.CalculatedAt = DateTime.UtcNow;
        context.Set<MemoryScore>().Add(score);
        await context.SaveChangesAsync();
    }

    public async Task AddBatchAsync(IReadOnlyList<MemoryScore> scores)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        foreach (var score in scores)
            score.CalculatedAt = now;
        context.Set<MemoryScore>().AddRange(scores);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(MemoryScore score)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        score.CalculatedAt = DateTime.UtcNow;
        context.Set<MemoryScore>().Update(score);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid memoryId, int photoId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var score = await context.Set<MemoryScore>().FindAsync(memoryId, photoId);
        if (score is not null)
        {
            context.Set<MemoryScore>().Remove(score);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteByMemoryAsync(Guid memoryId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var scores = await context.Set<MemoryScore>()
            .Where(s => s.MemoryId == memoryId)
            .ToListAsync();
        if (scores.Count > 0)
        {
            context.Set<MemoryScore>().RemoveRange(scores);
            await context.SaveChangesAsync();
        }
    }
}
