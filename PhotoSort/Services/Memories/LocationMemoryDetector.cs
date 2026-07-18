using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public sealed class LocationMemoryDetector : IMemoryDetector
{
    private readonly IPhotoRepository _photoRepo;
    private readonly ILogger<LocationMemoryDetector> _logger;

    public LocationMemoryDetector(IPhotoRepository photoRepo, ILogger<LocationMemoryDetector> logger)
    {
        _photoRepo = photoRepo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MemoryCandidate>> DetectCandidatesAsync(
        IReadOnlyList<int> photoIds, CancellationToken ct = default)
    {
        var candidates = new List<MemoryCandidate>();
        var geoClusters = new Dictionary<string, List<Models.Photo>>();

        foreach (var id in photoIds)
        {
            var photo = await _photoRepo.GetByIdAsync(id);
            if (photo?.Latitude is null || photo?.Longitude is null) continue;

            var hash = ComputeSimpleGeoHash(photo.Latitude.Value, photo.Longitude.Value);
            if (!geoClusters.ContainsKey(hash))
                geoClusters[hash] = [];
            geoClusters[hash].Add(photo);
        }

        foreach (var (hash, photos) in geoClusters)
        {
            if (photos.Count < 3) continue;

            candidates.Add(new MemoryCandidate
            {
                Type = MemoryType.Location,
                PhotoIds = photos.Select(p => p.Id).ToList(),
                DateStart = photos.Min(p => p.DateTaken ?? p.ModifiedDateUtc),
                DateEnd = photos.Max(p => p.DateTaken ?? p.ModifiedDateUtc),
                LocationHint = hash,
                Score = photos.Count * 0.08
            });
        }

        return candidates;
    }

    private static string ComputeSimpleGeoHash(double lat, double lng)
    {
        return $"{lat:F2},{lng:F2}";
    }
}
