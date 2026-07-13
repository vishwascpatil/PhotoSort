using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class PersonRepository : Repository<Person>, IPersonRepository
{
    public PersonRepository(IDbContextFactory<PhotoSortDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<Person?> GetByNameAsync(string name)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Person>()
            .FirstOrDefaultAsync(p => p.Name == name);
    }

    public async Task<IReadOnlyList<Person>> GetPeopleWithPhotoCountAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Person>()
            .Include(p => p.PersonFaces).ThenInclude(pf => pf.Face)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Person>> GetPeopleWithFaceCountAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Person>()
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Person?> GetWithFacesAsync(int personId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Person>()
            .Include(p => p.PersonFaces).ThenInclude(pf => pf.Face).ThenInclude(f => f.Photo)
            .Include(p => p.PersonFaces).ThenInclude(pf => pf.Face).ThenInclude(f => f.FaceEmbedding)
            .FirstOrDefaultAsync(p => p.Id == personId);
    }

    public async Task<int> GetPersonCountAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Person>().CountAsync();
    }

    public async Task<IReadOnlyList<int>> GetFaceIdsByPersonIdAsync(int personId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.PersonFaces
            .Where(pf => pf.PersonId == personId)
            .Select(pf => pf.FaceId)
            .ToListAsync();
    }

    public async Task<Person?> GetByFaceIdAsync(int faceId)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        var personFace = await context.PersonFaces
            .Include(pf => pf.Person)
            .FirstOrDefaultAsync(pf => pf.FaceId == faceId);

        return personFace?.Person;
    }
}
