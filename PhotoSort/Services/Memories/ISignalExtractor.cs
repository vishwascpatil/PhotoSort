using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public interface ISignalExtractor
{
    bool CanExtract(string extension);
    Task ExtractAsync(int photoId, CancellationToken ct = default);
}
