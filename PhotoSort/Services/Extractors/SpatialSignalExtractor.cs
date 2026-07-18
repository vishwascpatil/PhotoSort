using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;
using PhotoSort.Services.Memories;

namespace PhotoSort.Services.Extractors;

public sealed class SpatialSignalExtractor : ISignalExtractor
{
    private readonly IPhotoRepository _photoRepo;
    private readonly ILogger<SpatialSignalExtractor> _logger;

    public SpatialSignalExtractor(IPhotoRepository photoRepo, ILogger<SpatialSignalExtractor> logger)
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
            if (photo?.Latitude is null || photo?.Longitude is null)
                return;

            var signal = new SpatialSignal
            {
                Latitude = photo.Latitude,
                Longitude = photo.Longitude,
                GeoHash = ComputeGeoHash(photo.Latitude.Value, photo.Longitude.Value, 5),
                Weight = 0.5
            };

            var photoSignal = new PhotoSignal { PhotoId = photoId };
            photoSignal.SetTyped("spatial", signal);
            _logger.LogDebug("Extracted spatial signal for photo {PhotoId}", photoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract spatial signal for photo {PhotoId}", photoId);
        }
    }

    private static string ComputeGeoHash(double lat, double lng, int precision)
    {
        var chars = "0123456789bcdefghjkmnpqrstuvwxyz";
        var latRange = (-90.0, 90.0);
        var lngRange = (-180.0, 180.0);
        var hash = "";
        var isEven = true;
        var bit = 0;
        var ch = 0;

        while (hash.Length < precision)
        {
            if (isEven)
            {
                var mid = (lngRange.Item1 + lngRange.Item2) / 2;
                if (lng > mid) { ch |= (1 << (4 - bit)); lngRange.Item1 = mid; }
                else lngRange.Item2 = mid;
            }
            else
            {
                var mid = (latRange.Item1 + latRange.Item2) / 2;
                if (lat > mid) { ch |= (1 << (4 - bit)); latRange.Item1 = mid; }
                else latRange.Item2 = mid;
            }
            isEven = !isEven;
            if (bit < 4) { bit++; }
            else { hash += chars[ch]; bit = 0; ch = 0; }
        }
        return hash;
    }
}
