using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services.Memories;

namespace PhotoSort.Services.Extractors;

public sealed class BurstSignalExtractor : ISignalExtractor
{
    private readonly IPhotoRepository _photoRepo;
    private readonly ILogger<BurstSignalExtractor> _logger;

    public BurstSignalExtractor(IPhotoRepository photoRepo, ILogger<BurstSignalExtractor> logger)
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
            if (photo?.DateTaken is null) return;

            var signal = new BurstSignal
            {
                GroupCount = 1,
                BestPhotoId = photoId,
                AlternatePhotoIds = [],
                Weight = 0.03
            };
            var photoSignal = new PhotoSignal { PhotoId = photoId };
            photoSignal.SetTyped("burst", signal);
            _logger.LogDebug("Extracted burst signal for photo {PhotoId}", photoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract burst signal for photo {PhotoId}", photoId);
        }
    }
}
