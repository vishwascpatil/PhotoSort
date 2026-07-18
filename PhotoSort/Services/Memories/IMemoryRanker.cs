using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public interface IMemoryRanker
{
    Task<IReadOnlyList<Memory>> RankAsync(
        IReadOnlyList<MemoryCandidate> candidates,
        int topK = 50,
        CancellationToken ct = default);
}
