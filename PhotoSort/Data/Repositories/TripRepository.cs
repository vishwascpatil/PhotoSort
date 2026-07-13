using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class TripRepository : Repository<Trip>, ITripRepository
{
    public TripRepository(IDbContextFactory<PhotoSortDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IReadOnlyList<Trip>> GetAllWithDetailsAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Trip>()
            .Include(t => t.TripPhotos).ThenInclude(tp => tp.Photo)
            .Include(t => t.TripPlaces).ThenInclude(tp => tp.Place)
            .OrderByDescending(t => t.StartDate)
            .ToListAsync();
    }

    public async Task<Trip?> GetWithDetailsAsync(int tripId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Trip>()
            .Include(t => t.TripPhotos).ThenInclude(tp => tp.Photo)
            .Include(t => t.TripPlaces).ThenInclude(tp => tp.Place)
            .FirstOrDefaultAsync(t => t.Id == tripId);
    }

    public async Task<IReadOnlyList<TripPhoto>> GetTripPhotosAsync(int tripId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.TripPhotos
            .Where(tp => tp.TripId == tripId)
            .Include(tp => tp.Photo)
            .OrderBy(tp => tp.DateTaken)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TripPlace>> GetTripPlacesAsync(int tripId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.TripPlaces
            .Where(tp => tp.TripId == tripId)
            .Include(tp => tp.Place)
            .ToListAsync();
    }

    public async Task<int> GetTotalTripCountAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Trip>().CountAsync();
    }

    public async Task<IReadOnlyList<Trip>> GetTripsByDateRangeAsync(DateTime start, DateTime end)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Trip>()
            .Where(t => t.StartDate >= start && t.EndDate <= end)
            .Include(t => t.TripPhotos)
            .Include(t => t.TripPlaces)
            .OrderByDescending(t => t.StartDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Trip>> GetFavoriteTripsAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Trip>()
            .Where(t => t.IsFavorite)
            .Include(t => t.TripPhotos)
            .Include(t => t.TripPlaces)
            .OrderByDescending(t => t.StartDate)
            .ToListAsync();
    }

    public async Task<Trip?> GetTripForPhotoAsync(int photoId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var tripPhoto = await context.TripPhotos
            .Include(tp => tp.Trip)
            .FirstOrDefaultAsync(tp => tp.PhotoId == photoId);

        return tripPhoto?.Trip;
    }

    public async Task<bool> PhotoHasTripAsync(int photoId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.TripPhotos.AnyAsync(tp => tp.PhotoId == photoId);
    }
}
