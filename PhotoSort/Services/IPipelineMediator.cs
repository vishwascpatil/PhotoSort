using PhotoSort.Models;

namespace PhotoSort.Services;

public interface IPipelineMediator
{
    event EventHandler<PhotoMetadata>? MetadataExtracted;

    void NotifyMetadataExtracted(PhotoMetadata metadata);
}
