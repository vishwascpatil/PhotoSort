using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class MemoryRepository : IMemoryRepository
{
    private readonly IDbContextFactory<PhotoSortDbContext> _contextFactory;

    public MemoryRepository(IDbContextFactory<PhotoSortDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Memory> InsertAsync(Memory memory)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        memory.CreatedAt = DateTime.UtcNow;
        memory.UpdatedAt = DateTime.UtcNow;
        context.Set<Memory>().Add(memory);
        await context.SaveChangesAsync();
        return memory;
    }

    public async Task UpdateAsync(Memory memory)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        memory.UpdatedAt = DateTime.UtcNow;
        context.Set<Memory>().Update(memory);
        await context.SaveChangesAsync();
    }

    public async Task<Memory?> GetByIdAsync(Guid id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Set<Memory>().FindAsync(id);
    }

    public async Task<IReadOnlyList<Memory>> GetActiveMemoriesAsync(int count, int offset)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Set<Memory>()
            .Where(m => !m.Dismissed && !m.IsArchived)
            .OrderByDescending(m => m.Score)
            .Skip(offset)
            .Take(count)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Memory>> GetMemoriesByDateAsync(DateTime date)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var start = date.Date;
        var end = start.AddDays(1);
        return await context.Set<Memory>()
            .Where(m => m.DateStart >= start && m.DateStart < end && !m.Dismissed && !m.IsArchived)
            .OrderByDescending(m => m.Score)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<int> GetActiveCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Set<Memory>()
            .CountAsync(m => !m.Dismissed && !m.IsArchived);
    }

    public async Task ArchiveStaleAsync(DateTime threshold)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var stale = await context.Set<Memory>()
            .Where(m => m.LastShownAt != null && m.LastShownAt < threshold && !m.IsArchived)
            .ToListAsync();
        foreach (var m in stale)
            m.IsArchived = true;
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var memory = await context.Set<Memory>().FindAsync(id);
        if (memory is not null)
        {
            context.Set<Memory>().Remove(memory);
            await context.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<Memory>> GetOnThisDayAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var today = DateTime.UtcNow;
        return await context.Set<Memory>()
            .Where(m => m.DateStart.Month == today.Month && m.DateStart.Day == today.Day
                && m.DateStart.Year < today.Year - 1
                && !m.Dismissed && !m.IsArchived)
            .OrderByDescending(m => m.Score)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task BulkInsertAsync(IReadOnlyList<Memory> memories)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        foreach (var memory in memories)
        {
            memory.CreatedAt = DateTime.UtcNow;
            memory.UpdatedAt = DateTime.UtcNow;
            context.Set<Memory>().Add(memory);
        }
        await context.SaveChangesAsync();
    }
}
