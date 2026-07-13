using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface ITagRepository : IRepository<Tag>
{
    Task<Tag?> GetByNameAsync(string name);
    Task<IReadOnlyList<Tag>> GetTagsWithPhotoCountAsync();
    Task<Tag> GetOrCreateAsync(string name);
}
