using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class PlaceRepository : Repository<Place>, IPlaceRepository
{
    public PlaceRepository(IDbContextFactory<PhotoSortDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<Place?> GetByNameAsync(string name)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Place>()
            .FirstOrDefaultAsync(p => p.Name == name);
    }

    public async Task<IReadOnlyList<Place>> GetAllWithPhotoCountAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Place>()
            .Include(p => p.PhotoPlaces)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<int> GetTotalPlaceCountAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Place>().CountAsync();
    }

    public async Task<IReadOnlyList<Place>> GetPlacesWithGpsAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Place>()
            .Where(p => p.Latitude.HasValue && p.Longitude.HasValue)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<(int PlaceId, int PhotoCount, DateTime? FirstDate, DateTime? LastDate)>> GetPlaceStatsAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.PhotoPlaces
            .GroupBy(pp => pp.PlaceId)
            .Select(g => new
            {
                PlaceId = g.Key,
                PhotoCount = g.Count(),
                FirstDate = g.Min(pp => pp.Photo.DateTaken),
                LastDate = g.Max(pp => pp.Photo.DateTaken)
            })
            .Select(x => new ValueTuple<int, int, DateTime?, DateTime?>(
                x.PlaceId, x.PhotoCount, x.FirstDate, x.LastDate))
            .ToListAsync();
    }
}
