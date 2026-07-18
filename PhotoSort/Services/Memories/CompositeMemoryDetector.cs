using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public sealed class CompositeMemoryDetector : IMemoryDetector
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CompositeMemoryDetector> _logger;

    public CompositeMemoryDetector(
        IServiceProvider serviceProvider,
        ILogger<CompositeMemoryDetector> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    private IEnumerable<IMemoryDetector> GetLeafDetectors()
    {
        return _serviceProvider.GetServices<IMemoryDetector>()
            .Where(d => d.GetType() != typeof(CompositeMemoryDetector));
    }

    public async Task<IReadOnlyList<MemoryCandidate>> DetectCandidatesAsync(
        IReadOnlyList<int> photoIds, CancellationToken ct = default)
    {
        var allCandidates = new List<MemoryCandidate>();

        foreach (var detector in GetLeafDetectors())
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var candidates = await detector.DetectCandidatesAsync(photoIds, ct);
                allCandidates.AddRange(candidates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Detector {Detector} failed", detector.GetType().Name);
            }
        }

        // Deduplicate overlapping candidates (merge if >50% photo overlap)
        var merged = MergeOverlapping(allCandidates);

        _logger.LogDebug("Composite detector: {Input} candidates → {Merged} after dedup",
            allCandidates.Count, merged.Count);
        return merged;
    }

    private static List<MemoryCandidate> MergeOverlapping(List<MemoryCandidate> candidates)
    {
        var merged = new List<MemoryCandidate>();
        var used = new HashSet<Guid>();

        foreach (var c in candidates.OrderByDescending(c => c.Score))
        {
            if (used.Contains(c.Id)) continue;

            var toMerge = candidates
                .Where(x => !used.Contains(x.Id) && x.Id != c.Id)
                .Where(x => OverlapRatio(c.PhotoIds, x.PhotoIds) > 0.5)
                .ToList();

            foreach (var m in toMerge)
            {
                c.PhotoIds = c.PhotoIds.Union(m.PhotoIds).ToList();
                c.PersonIds = c.PersonIds.Union(m.PersonIds).ToList();
                c.Score = Math.Max(c.Score, m.Score);
                used.Add(m.Id);
            }

            used.Add(c.Id);
            merged.Add(c);
        }

        return merged;
    }

    private static double OverlapRatio(List<int> a, List<int> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var intersection = a.Intersect(b).Count();
        var union = a.Union(b).Count();
        return (double)intersection / union;
    }
}
