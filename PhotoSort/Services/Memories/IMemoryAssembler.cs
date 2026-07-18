using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public interface IMemoryAssembler
{
    Task<Memory> AssembleAsync(MemoryCandidate candidate, CancellationToken ct = default);
}
