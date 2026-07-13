using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class FaceRepository : Repository<Face>, IFaceRepository
{
    public FaceRepository(IDbContextFactory<PhotoSortDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IReadOnlyList<Face>> GetByPhotoIdAsync(int photoId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Face>()
            .Where(f => f.PhotoId == photoId)
            .Include(f => f.PersonFaces).ThenInclude(pf => pf.Person)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Face>> GetByPersonIdAsync(int personId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Face>()
            .Where(f => f.PersonFaces.Any(pf => pf.PersonId == personId))
            .Include(f => f.Photo)
            .Include(f => f.PersonFaces).ThenInclude(pf => pf.Person)
            .OrderByDescending(f => f.CreatedDate)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Face>> GetUnassignedFacesAsync(int limit = 1000)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Face>()
            .Where(f => !f.PersonFaces.Any() && !f.IsIgnored && f.FaceEmbedding != null)
            .Include(f => f.Photo)
            .Include(f => f.FaceEmbedding)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Face>> GetFacesWithEmbeddingsAsync(int limit = 1000)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Face>()
            .Where(f => f.FaceEmbedding != null && !f.IsIgnored)
            .Include(f => f.Photo)
            .Include(f => f.FaceEmbedding)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> GetFaceCountByPersonIdAsync(int personId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Face>()
            .CountAsync(f => f.PersonFaces.Any(pf => pf.PersonId == personId) && !f.IsIgnored);
    }

    public async Task<int> GetTotalFaceCountAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Face>().CountAsync();
    }

    public async Task<int> GetAssignedFaceCountAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Face>()
            .CountAsync(f => f.PersonFaces.Any() && !f.IsIgnored);
    }

    public async Task<Face?> GetWithDetailsAsync(int faceId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Face>()
            .Include(f => f.Photo)
            .Include(f => f.PersonFaces).ThenInclude(pf => pf.Person)
            .Include(f => f.FaceEmbedding)
            .FirstOrDefaultAsync(f => f.Id == faceId);
    }

    public async Task<IReadOnlyList<Face>> GetIgnoredFacesAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Face>()
            .Where(f => f.IsIgnored)
            .Include(f => f.Photo)
            .ToListAsync();
    }

    public async Task<int> GetIgnoredFaceCountAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Face>().CountAsync(f => f.IsIgnored);
    }

    public async Task<int> GetFaceCountForPhotoAsync(int photoId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Face>().CountAsync(f => f.PhotoId == photoId);
    }
}
