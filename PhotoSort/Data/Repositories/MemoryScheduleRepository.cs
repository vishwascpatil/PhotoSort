using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class MemoryScheduleRepository : IMemoryScheduleRepository
{
    private readonly IDbContextFactory<PhotoSortDbContext> _contextFactory;

    public MemoryScheduleRepository(IDbContextFactory<PhotoSortDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task InsertAsync(MemorySchedule schedule)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Set<MemorySchedule>().Add(schedule);
        await context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<MemorySchedule>> GetDueSchedulesAsync(DateTime now)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Set<MemorySchedule>()
            .Where(s => s.ScheduleDate <= now
                && (s.LastShownAt == null || s.LastShownAt < now.Date))
            .OrderBy(s => s.ScheduleDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task MarkShownAsync(int scheduleId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var schedule = await context.Set<MemorySchedule>().FindAsync(scheduleId);
        if (schedule is not null)
        {
            schedule.LastShownAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }
}
