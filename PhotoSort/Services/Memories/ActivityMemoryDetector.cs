using Microsoft.Extensions.Logging;
using PhotoSort.Data.Repositories;
using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public sealed class ActivityMemoryDetector : IMemoryDetector
{
    private readonly IPhotoRepository _photoRepo;
    private readonly ILogger<ActivityMemoryDetector> _logger;

    public ActivityMemoryDetector(IPhotoRepository photoRepo, ILogger<ActivityMemoryDetector> logger)
    {
        _photoRepo = photoRepo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MemoryCandidate>> DetectCandidatesAsync(
        IReadOnlyList<int> photoIds, CancellationToken ct = default)
    {
        var candidates = new List<MemoryCandidate>();
        if (photoIds.Count < 3) return candidates;

        var photos = new List<Models.Photo>();
        foreach (var id in photoIds)
        {
            var p = await _photoRepo.GetByIdAsync(id);
            if (p is not null) photos.Add(p);
        }

        if (photos.Count < 3) return candidates;

        var sorted = photos.OrderBy(p => p.DateTaken ?? p.ModifiedDateUtc).ToList();
        var clusters = new List<List<Models.Photo>>();
        var current = new List<Models.Photo> { sorted[0] };

        for (int i = 1; i < sorted.Count; i++)
        {
            var gap = (sorted[i].DateTaken ?? sorted[i].ModifiedDateUtc)
                - (sorted[i - 1].DateTaken ?? sorted[i - 1].ModifiedDateUtc);
            if (gap.TotalHours <= 4)
                current.Add(sorted[i]);
            else
            {
                if (current.Count >= 3)
                    clusters.Add([.. current]);
                current = [sorted[i]];
            }
        }
        if (current.Count >= 3) clusters.Add(current);

        foreach (var cluster in clusters)
        {
            candidates.Add(new MemoryCandidate
            {
                Type = MemoryType.Activity,
                PhotoIds = cluster.Select(p => p.Id).ToList(),
                DateStart = cluster.Min(p => p.DateTaken ?? p.ModifiedDateUtc),
                DateEnd = cluster.Max(p => p.DateTaken ?? p.ModifiedDateUtc),
                ActivityHint = "Moments",
                Score = cluster.Count * 0.07
            });
        }

        return candidates;
    }
}
