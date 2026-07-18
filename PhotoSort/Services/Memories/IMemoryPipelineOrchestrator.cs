using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public interface IMemoryPipelineOrchestrator
{
    Task RunFullPipelineAsync(CancellationToken ct = default);
    Task RunIncrementalAsync(int photoId, CancellationToken ct = default);
    Task RunBatchAsync(IReadOnlyList<int> photoIds, CancellationToken ct = default);
    Task ReScoreAllAsync(CancellationToken ct = default);
}
