using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services.Memories;

namespace PhotoSort.Services.Extractors;

public sealed class WeatherSignalExtractor : ISignalExtractor
{
    private readonly IPhotoRepository _photoRepo;
    private readonly ILogger<WeatherSignalExtractor> _logger;

    public WeatherSignalExtractor(IPhotoRepository photoRepo, ILogger<WeatherSignalExtractor> logger)
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
            if (photo?.Latitude is null || photo?.Longitude is null || photo?.DateTaken is null)
                return;

            var signal = new WeatherSignal
            {
                Condition = "unknown",
                TemperatureC = 20,
                Weight = 0.5
            };
            var photoSignal = new PhotoSignal { PhotoId = photoId };
            photoSignal.SetTyped("weather", signal);
            _logger.LogDebug("Extracted weather signal for photo {PhotoId}", photoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract weather signal for photo {PhotoId}", photoId);
        }
    }
}
