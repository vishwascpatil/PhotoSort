using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services.Memories;

namespace PhotoSort.Services.Extractors;

public sealed class OcrSignalExtractor : ISignalExtractor
{
    private readonly ILogger<OcrSignalExtractor> _logger;
    private static readonly HashSet<string> ImageExts = [".jpg", ".jpeg", ".png", ".webp"];

    public OcrSignalExtractor(ILogger<OcrSignalExtractor> logger)
    {
        _logger = logger;
    }

    public bool CanExtract(string extension) => ImageExts.Contains(extension.ToLowerInvariant());

    public Task ExtractAsync(int photoId, CancellationToken ct = default)
    {
        _logger.LogDebug("OCR extraction placeholder for photo {PhotoId}", photoId);
        return Task.CompletedTask;
    }
}
