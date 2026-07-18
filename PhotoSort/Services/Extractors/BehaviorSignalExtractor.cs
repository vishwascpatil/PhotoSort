using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services.Memories;

namespace PhotoSort.Services.Extractors;

public sealed class BehaviorSignalExtractor : ISignalExtractor
{
    private readonly IPhotoRepository _photoRepo;
    private readonly ILogger<BehaviorSignalExtractor> _logger;

    public BehaviorSignalExtractor(IPhotoRepository photoRepo, ILogger<BehaviorSignalExtractor> logger)
    {
        _photoRepo = photoRepo;
        _logger = logger;
    }

    public bool CanExtract(string extension) => true;

    public async Task ExtractAsync(int photoId, CancellationToken ct = default)
    {
        try
        {
            var photo = await _photoRepo.GetByIdAsync(photoId);
            if (photo is null) return;

            var signal = new BehaviorSignal
            {
                IsFavorite = photo.IsFavorite,
                EngagementScore = photo.IsFavorite ? 1.0 : 0.3,
                Weight = photo.IsFavorite ? 1.0 : 0.3
            };
            var photoSignal = new PhotoSignal { PhotoId = photoId };
            photoSignal.SetTyped("behavior", signal);
            _logger.LogDebug("Extracted behavior signal for photo {PhotoId}", photoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract behavior signal for photo {PhotoId}", photoId);
        }
    }
}
