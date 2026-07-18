using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public sealed class TemporalMemoryDetector : IMemoryDetector
{
    private readonly IPhotoRepository _photoRepo;
    private readonly ILogger<TemporalMemoryDetector> _logger;
    private static readonly MemoryType[] OrderedTypes = [MemoryType.Day, MemoryType.Week, MemoryType.Month, MemoryType.Season];

    public TemporalMemoryDetector(IPhotoRepository photoRepo, ILogger<TemporalMemoryDetector> logger)
    {
        _photoRepo = photoRepo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MemoryCandidate>> DetectCandidatesAsync(
        IReadOnlyList<int> photoIds, CancellationToken ct = default)
    {
        var candidates = new List<MemoryCandidate>();
        if (photoIds.Count == 0) return candidates;

        var photos = new List<Models.Photo>();
        foreach (var id in photoIds)
        {
            var p = await _photoRepo.GetByIdAsync(id);
            if (p?.DateTaken is not null) photos.Add(p);
        }
        if (photos.Count < 3) return candidates;

        var sorted = photos.OrderBy(p => p.DateTaken).ToList();
        var now = DateTime.UtcNow;

        // On This Day
        var todayPhotos = sorted
            .Where(p => p.DateTaken!.Value.Month == now.Month && p.DateTaken!.Value.Day == now.Day
                && p.DateTaken!.Value.Year < now.Year - 1)
            .ToList();
        if (todayPhotos.Count >= 2)
        {
            candidates.Add(new MemoryCandidate
            {
                Type = MemoryType.Day,
                PhotoIds = todayPhotos.Select(p => p.Id).ToList(),
                DateStart = todayPhotos.Min(p => p.DateTaken!.Value),
                DateEnd = todayPhotos.Max(p => p.DateTaken!.Value),
                ActivityHint = "On This Day",
                Score = todayPhotos.Count * 0.1
            });
        }

        // This Week Last Year
        var weekAgo = now.AddDays(-7);
        var weekPhotos = sorted
            .Where(p => p.DateTaken >= weekAgo && p.DateTaken < now && p.DateTaken.Value.Year < now.Year - 1)
            .ToList();
        if (weekPhotos.Count >= 3)
        {
            candidates.Add(new MemoryCandidate
            {
                Type = MemoryType.Week,
                PhotoIds = weekPhotos.Select(p => p.Id).ToList(),
                DateStart = weekPhotos.Min(p => p.DateTaken!.Value),
                DateEnd = weekPhotos.Max(p => p.DateTaken!.Value),
                ActivityHint = "This Week",
                Score = weekPhotos.Count * 0.08
            });
        }

        // Monthly highlights (last 12 months from previous years)
        foreach (var monthOffset in Enumerable.Range(1, 12))
        {
            var targetMonth = now.AddMonths(-monthOffset);
            var monthPhotos = sorted
                .Where(p => p.DateTaken!.Value.Year == targetMonth.Year
                    && p.DateTaken!.Value.Month == targetMonth.Month
                    && targetMonth.Year < now.Year - 1)
                .ToList();
            if (monthPhotos.Count >= 4)
            {
                candidates.Add(new MemoryCandidate
                {
                    Type = MemoryType.Month,
                    PhotoIds = monthPhotos.Select(p => p.Id).ToList(),
                    DateStart = monthPhotos.Min(p => p.DateTaken!.Value),
                    DateEnd = monthPhotos.Max(p => p.DateTaken!.Value),
                    ActivityHint = $"{targetMonth:MMMM yyyy}",
                    Score = monthPhotos.Count * 0.06
                });
            }
        }

        _logger.LogDebug("Detected {Count} temporal memory candidates from {PhotoCount} photos",
            candidates.Count, photoIds.Count);
        return candidates;
    }
}
