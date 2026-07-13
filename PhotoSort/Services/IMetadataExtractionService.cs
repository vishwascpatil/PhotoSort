using PhotoSort.Models;

namespace PhotoSort.Services;

public interface IMetadataExtractionService
{
    bool IsExtracting { get; }

    Task<ExtractionResult> ExtractMetadataAsync(
        int folderId,
        IProgress<MetadataExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ExtractionResult> ExtractAllAsync(
        IProgress<MetadataExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task CancelExtractionAsync();
}
