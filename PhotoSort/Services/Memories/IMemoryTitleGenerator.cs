using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public interface IMemoryTitleGenerator
{
    string GenerateTitle(MemoryCandidate candidate);
}
