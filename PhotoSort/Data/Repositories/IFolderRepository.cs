using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IFolderRepository : IRepository<Folder>
{
    Task<Folder?> GetByFolderPathAsync(string folderPath);
    Task<IReadOnlyList<Folder>> GetFoldersWithPhotosAsync();
    Task<bool> ExistsByPathAsync(string folderPath);
}
