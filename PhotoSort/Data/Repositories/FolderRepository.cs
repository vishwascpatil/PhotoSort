using Microsoft.EntityFrameworkCore;
using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public sealed class FolderRepository : Repository<Folder>, IFolderRepository
{
    public FolderRepository(IDbContextFactory<PhotoSortDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<Folder?> GetByFolderPathAsync(string folderPath)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Folder>()
            .FirstOrDefaultAsync(f => f.FolderPath == folderPath);
    }

    public async Task<IReadOnlyList<Folder>> GetFoldersWithPhotosAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Folder>()
            .Include(f => f.Photos)
            .OrderByDescending(f => f.AddedDate)
            .ToListAsync();
    }

    public async Task<bool> ExistsByPathAsync(string folderPath)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<Folder>().AnyAsync(f => f.FolderPath == folderPath);
    }
}
