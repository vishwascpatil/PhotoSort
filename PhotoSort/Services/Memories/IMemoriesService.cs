using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public interface IMemoriesService
{
    Task<IReadOnlyList<Memory>> GetMemoriesAsync(int count = 20, int offset = 0, CancellationToken ct = default);
    Task<Memory?> GetMemoryByIdAsync(Guid memoryId);
    Task DismissMemoryAsync(Guid memoryId);
    Task SnoozeMemoryAsync(Guid memoryId, TimeSpan duration);
    Task<IReadOnlyList<Memory>> GetMemoriesByDateAsync(DateTime date, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync();
    Task<IReadOnlyList<Memory>> GetOnThisDayAsync(CancellationToken ct = default);
    Task RunPipelineAsync(CancellationToken ct = default);
}
