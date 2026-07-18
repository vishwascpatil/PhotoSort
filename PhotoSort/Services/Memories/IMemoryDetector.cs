using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public interface IMemoryDetector
{
    Task<IReadOnlyList<MemoryCandidate>> DetectCandidatesAsync(
        IReadOnlyList<int> photoIds,
        CancellationToken ct = default);
}
