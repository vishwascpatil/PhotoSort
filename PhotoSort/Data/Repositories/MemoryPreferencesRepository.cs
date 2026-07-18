using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class MemoryPreferencesRepository : Repository<MemoryPreferences>, IMemoryPreferencesRepository
{
    public MemoryPreferencesRepository(IDbContextFactory<PhotoSortDbContext> contextFactory)
        : base(contextFactory)
    {
    }

    public async Task<MemoryPreferences?> GetSingleAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<MemoryPreferences>()
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    public async Task<MemoryPreferences> EnsureSingleAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var existing = await context.Set<MemoryPreferences>().FirstOrDefaultAsync();
        if (existing is not null)
            return existing;

        var prefs = new MemoryPreferences();
        context.Set<MemoryPreferences>().Add(prefs);
        await context.SaveChangesAsync();
        return prefs;
    }
}
