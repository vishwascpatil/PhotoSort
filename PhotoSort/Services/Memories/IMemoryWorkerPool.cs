using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public interface IMemoryWorkerPool
{
    Task EnqueueSignalExtractionAsync(int photoId, CancellationToken ct = default);
    Task EnqueueCandidateGenerationAsync(IReadOnlyList<int> photoIds, CancellationToken ct = default);
    Task EnqueueScoringAsync(MemoryCandidate candidate, CancellationToken ct = default);
    Task EnqueueAssemblyAsync(MemoryCandidate candidate, CancellationToken ct = default);
    Task WaitForCompletionAsync(CancellationToken ct = default);
}
