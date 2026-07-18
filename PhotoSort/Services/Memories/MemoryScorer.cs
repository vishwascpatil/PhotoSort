using Microsoft.Extensions.Options;
using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public sealed class MemoryScorer : IMemoryScorer
{
    private readonly MemoryConfig _config;

    public MemoryScorer(IOptions<MemoryConfig> config)
    {
        _config = config.Value;
    }

    public Task<double> ComputeScoreAsync(MemoryCandidate candidate, CancellationToken ct = default)
    {
        var w = _config.Scoring;
        var score = 0.0;

        // Temporal score
        score += w.Temporal * Math.Min(1.0, candidate.PhotoIds.Count * 0.02);

        // Social score
        if (candidate.PersonIds.Count > 0)
            score += w.Social * Math.Min(1.0, candidate.PersonIds.Count * 0.15);

        // Quality score (baseline from photo count as proxy)
        score += w.Quality * Math.Min(1.0, candidate.PhotoIds.Count * 0.01);

        // Semantic/Activity score
        if (!string.IsNullOrEmpty(candidate.ActivityHint))
            score += w.Semantic * 0.5;

        // Location score
        if (!string.IsNullOrEmpty(candidate.LocationHint))
            score += w.Location * 0.5;

        // Holiday boost
        if (!string.IsNullOrEmpty(candidate.HolidayHint))
            score += 0.15;

        // Size normalization (log scale)
        var sizeScore = Math.Log10(candidate.PhotoIds.Count + 1) / Math.Log10(101);
        score *= (0.7 + 0.3 * sizeScore);

        candidate.Score = Math.Clamp(score, 0, 1.0);
        return Task.FromResult(candidate.Score);
    }

    public Task InvalidateScoreAsync(int photoId) => Task.CompletedTask;
}
