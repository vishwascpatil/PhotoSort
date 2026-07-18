using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public interface IMemoryScorer
{
    Task<double> ComputeScoreAsync(MemoryCandidate candidate, CancellationToken ct = default);
    Task InvalidateScoreAsync(int photoId);
}
