using PhotoSort.Models;

namespace PhotoSort.Data.Repositories;

public interface IMemoryFeedbackRepository : IRepository<MemoryFeedback>
{
    Task<IReadOnlyList<MemoryFeedback>> GetByMemoryAsync(Guid memoryId);

    Task<IReadOnlyList<MemoryFeedback>> GetRecentAsync(int count);
}
