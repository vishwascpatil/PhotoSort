using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class TagRepository : Repository<Tag>, ITagRepository
{
    public TagRepository(IDbContextFactory<PhotoSortDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<Tag?> GetByNameAsync(string name)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Tag>()
            .FirstOrDefaultAsync(t => t.Name == name);
    }

    public async Task<IReadOnlyList<Tag>> GetTagsWithPhotoCountAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Tag>()
            .Include(t => t.PhotoTags)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<Tag> GetOrCreateAsync(string name)
    {
        var existing = await GetByNameAsync(name);
        if (existing is not null)
            return existing;

        var tag = new Tag { Name = name };
        await AddAsync(tag);
        return tag;
    }
}
