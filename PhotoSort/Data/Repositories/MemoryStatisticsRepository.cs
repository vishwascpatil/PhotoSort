using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class MemoryStatisticsRepository : IMemoryStatisticsRepository
{
    private readonly IDbContextFactory<PhotoSortDbContext> _contextFactory;

    public MemoryStatisticsRepository(IDbContextFactory<PhotoSortDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<MemoryStatistics?> GetByTypeAsync(int memoryTypeId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Set<MemoryStatistics>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.MemoryTypeId == memoryTypeId);
    }

    public async Task<IReadOnlyList<MemoryStatistics>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Set<MemoryStatistics>()
            .Include(s => s.MemoryType)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddAsync(MemoryStatistics statistics)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        statistics.UpdatedAt = DateTime.UtcNow;
        context.Set<MemoryStatistics>().Add(statistics);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(MemoryStatistics statistics)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        statistics.UpdatedAt = DateTime.UtcNow;
        context.Set<MemoryStatistics>().Update(statistics);
        await context.SaveChangesAsync();
    }

    public async Task AddOrUpdateAsync(MemoryStatistics statistics)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        statistics.UpdatedAt = DateTime.UtcNow;
        var existing = await context.Set<MemoryStatistics>()
            .FindAsync(statistics.MemoryTypeId);

        if (existing is not null)
        {
            existing.TotalGenerated = statistics.TotalGenerated;
            existing.TotalViewed = statistics.TotalViewed;
            existing.AvgScore = statistics.AvgScore;
            existing.LastGeneratedAt = statistics.LastGeneratedAt;
            existing.UserViewCount = statistics.UserViewCount;
            existing.UserDwellSeconds = statistics.UserDwellSeconds;
            existing.UserFavoriteCount = statistics.UserFavoriteCount;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            context.Set<MemoryStatistics>().Add(statistics);
        }

        await context.SaveChangesAsync();
    }
}
