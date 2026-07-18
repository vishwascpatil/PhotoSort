using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services.Memories;

namespace PhotoSort.Services.Extractors;

public sealed class CameraSignalExtractor : ISignalExtractor
{
    private readonly IPhotoRepository _photoRepo;
    private readonly ILogger<CameraSignalExtractor> _logger;
    private static readonly HashSet<string> DslrMakes = ["Canon", "Nikon", "Sony", "Fuji", "Panasonic", "Olympus", "Pentax"];

    public CameraSignalExtractor(IPhotoRepository photoRepo, ILogger<CameraSignalExtractor> logger)
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

            var make = photo.CameraMake ?? "";
            var signal = new CameraSignal
            {
                Make = make,
                Model = photo.CameraModel,
                IsDSLR = DslrMakes.Contains(make, StringComparer.OrdinalIgnoreCase),

                Weight = DslrMakes.Contains(make, StringComparer.OrdinalIgnoreCase) ? 1.15 : 1.0
            };
            var photoSignal = new PhotoSignal { PhotoId = photoId };
            photoSignal.SetTyped("camera", signal);
            _logger.LogDebug("Extracted camera signal for photo {PhotoId}", photoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract camera signal for photo {PhotoId}", photoId);
        }
    }
}
