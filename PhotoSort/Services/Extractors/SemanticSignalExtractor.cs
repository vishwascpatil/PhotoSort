using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services.Memories;

namespace PhotoSort.Services.Extractors;

public sealed class SemanticSignalExtractor : ISignalExtractor
{
    private readonly IPhotoRepository _photoRepo;
    private readonly ILogger<SemanticSignalExtractor> _logger;
    private static readonly HashSet<string> ImageExts = [".jpg", ".jpeg", ".png", ".webp"];

    public SemanticSignalExtractor(IPhotoRepository photoRepo, ILogger<SemanticSignalExtractor> logger)
    {
        _photoRepo = photoRepo;
        _logger = logger;
    }

    public bool CanExtract(string extension) => ImageExts.Contains(extension.ToLowerInvariant());

    public async Task ExtractAsync(int photoId, CancellationToken ct = default)
    {
        try
        {
            var photo = await _photoRepo.GetByIdAsync(photoId);
            if (photo is null) return;

            var signal = new SemanticSignal
            {
                SceneType = "unknown",
                SceneConfidence = 0.5,
                DetectedObjects = [],
                Attributes = [],
                HasNature = false,
                HasCelebration = false,
                HasFood = false,
                HasPet = false
            };
            var photoSignal = new PhotoSignal { PhotoId = photoId };
            photoSignal.SetTyped("semantic", signal);
            _logger.LogDebug("Extracted semantic signal for photo {PhotoId}", photoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract semantic signal for photo {PhotoId}", photoId);
        }
    }
}
