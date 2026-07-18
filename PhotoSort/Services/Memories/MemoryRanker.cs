using Microsoft.Extensions.Options;
using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public sealed class MemoryRanker : IMemoryRanker
{
    private readonly IMemoryAssembler _assembler;
    private readonly MemoryConfig _config;

    public MemoryRanker(IMemoryAssembler assembler, IOptions<MemoryConfig> config)
    {
        _assembler = assembler;
        _config = config.Value;
    }

    public async Task<IReadOnlyList<Memory>> RankAsync(
        IReadOnlyList<MemoryCandidate> candidates,
        int topK = 50,
        CancellationToken ct = default)
    {
        if (candidates.Count == 0) return [];

        var sorted = candidates.OrderByDescending(c => c.Score).ToList();
        var selected = new List<MemoryCandidate>();
        var usedDates = new HashSet<DateTime>();
        var usedPersons = new HashSet<int>();
        var usedLocations = new HashSet<string>();

        foreach (var c in sorted)
        {
            if (selected.Count >= topK) break;

            // Diversity: max 3 per day
            var dayKey = c.DateStart.Date;
            if (usedDates.Count(d => d == dayKey) >= 3) continue;

            // Diversity: max 2 per person
            if (c.PersonIds.Any(p => usedPersons.Count(up => up == p) >= 2)) continue;

            // Diversity: max 1 per location
            if (c.LocationHint is not null && usedLocations.Contains(c.LocationHint)) continue;

            selected.Add(c);
            usedDates.Add(dayKey);
            foreach (var p in c.PersonIds) usedPersons.Add(p);
            if (c.LocationHint is not null) usedLocations.Add(c.LocationHint);
        }

        // Assemble final Memory objects
        var memories = new List<Memory>();
        foreach (var c in selected)
        {
            var m = await _assembler.AssembleAsync(c, ct);
            memories.Add(m);
        }

        return memories;
    }
}
