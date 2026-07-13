using Microsoft.Extensions.Logging;
using PhotoSort.Models;

namespace PhotoSort.Services;

public sealed class PipelineMediator : IPipelineMediator
{
    private readonly ILogger<PipelineMediator> _logger;

    public event EventHandler<PhotoMetadata>? MetadataExtracted;

    public PipelineMediator(ILogger<PipelineMediator> logger)
    {
        _logger = logger;
    }

    public void NotifyMetadataExtracted(PhotoMetadata metadata)
    {
        try
        {
            MetadataExtracted?.Invoke(this, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error notifying pipeline consumers for: {FilePath}", metadata.FilePath);
        }
    }
}
