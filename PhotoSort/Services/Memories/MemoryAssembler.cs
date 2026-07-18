using PhotoSort.Models;

namespace PhotoSort.Services.Memories;

public sealed class MemoryAssembler : IMemoryAssembler
{
    private readonly IMemoryTitleGenerator _titleGenerator;

    public MemoryAssembler(IMemoryTitleGenerator titleGenerator)
    {
        _titleGenerator = titleGenerator;
    }

    public Task<Memory> AssembleAsync(MemoryCandidate candidate, CancellationToken ct = default)
    {
        var memory = new Memory
        {
            Id = candidate.Id,
            Type = candidate.Type,
            Title = _titleGenerator.GenerateTitle(candidate),
            CoverPhotoId = candidate.PhotoIds.FirstOrDefault(),
            DateStart = candidate.DateStart == default ? DateTime.UtcNow : candidate.DateStart,
            DateEnd = candidate.DateEnd == default ? DateTime.UtcNow : candidate.DateEnd,
            LocationSummary = candidate.LocationHint,
            PeopleSummary = candidate.PersonIds.Count > 0 ? $"With friends" : null,
            Score = candidate.Score,
            IsGenerated = true,
            GeneratedAt = DateTime.UtcNow,
            Photos = candidate.PhotoIds.Select((pid, i) => new MemoryPhoto
            {
                MemoryId = candidate.Id,
                PhotoId = pid,
                SortOrder = i,
                Role = i == 0 ? "Cover" : "Supporting"
            }).ToList(),
            PersonIds = [.. candidate.PersonIds]
        };

        // Generate subtitle
        var dayCount = (memory.DateEnd - memory.DateStart).Days + 1;
        var photoCount = candidate.PhotoIds.Count;
        memory.Subtitle = $"{photoCount} photo{(photoCount != 1 ? "s" : "")}";

        if (dayCount > 1)
            memory.Subtitle += $" \u2022 {dayCount} day{(dayCount != 1 ? "s" : "")}";

        return Task.FromResult(memory);
    }
}
