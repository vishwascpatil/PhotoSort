using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public interface IMemoryCache
{
    Task<Memory?> GetAsync(Guid id);
    Task SetAsync(Guid id, Memory memory, TimeSpan? ttl = null);
    Task InvalidateAsync(Guid id);
    Task InvalidateByPhotoAsync(int photoId);
    Task WarmAsync(IReadOnlyList<Memory> memories);
    void Clear();
}
